// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher.Model;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Helix.Sdk;

[MSBuildMultiThreadableTask]
public class UploadHelixTestResults : HelixTask, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;

    [Required]
    public ITaskItem[] WorkItems { get; set; }

    [Required]
    public string OutputDirectory { get; set; }

    protected override async Task ExecuteCore(CancellationToken cancellationToken)
    {
        string collectionUri = GetRequiredEnvironmentVariable("SYSTEM_TEAMFOUNDATIONCOLLECTIONURI");
        string teamProject = GetRequiredEnvironmentVariable("SYSTEM_TEAMPROJECT");
        string token = GetRequiredEnvironmentVariable("SYSTEM_ACCESSTOKEN");
        string outputDirectory = TaskEnvironment.GetAbsolutePath(OutputDirectory);
        var logger = new BuildLogger(Log);
        var metrics = new TestReportingMetrics();
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("unused:" + token)));
        client.DefaultRequestHeaders.UserAgent.Add(Helpers.UserAgentHeaderValue);
        var transport = new AzureDevOpsResultTransport(collectionUri, teamProject, client, logger, metrics);
        var downloader = new HelixTestResultsDownloader(HelixApi, logger, metrics);
        var processor = new TestResultProcessor(TestResultAttachmentMode.Failed, useFullyQualifiedTestName: false, logger, metrics,
            failOnParseError: true, qualifyTrxTestNames: true);
        var publisher = new AzureDevOpsResultPublisher(logger, useFullyQualifiedTestName: false, transport, metrics);

        await UploadAsync(
            (workItem, ct) => downloader.DownloadAsync(
                workItem.GetMetadata("JobName"), workItem.GetMetadata("WorkItemName"), outputDirectory, ct),
            processor, publisher, cancellationToken);
    }

    internal async Task UploadAsync(
        Func<ITaskItem, CancellationToken, Task<WorkItemTestResults>> download,
        ITestResultProcessor processor,
        IAzureDevOpsResultPublisher publisher,
        CancellationToken cancellationToken)
    {
        foreach (ITaskItem workItem in WorkItems)
        {
            if (string.IsNullOrEmpty(workItem.GetMetadata("JobName")) ||
                string.IsNullOrEmpty(workItem.GetMetadata("WorkItemName")) ||
                !int.TryParse(workItem.GetMetadata("TestRunId"), NumberStyles.None, CultureInfo.InvariantCulture, out int runId) ||
                runId <= 0)
            {
                throw new ArgumentException($"Work item '{workItem.ItemSpec}' must specify JobName, WorkItemName, and a positive TestRunId.");
            }
        }

        Log.LogMessage(MessageImportance.High,
            $"Processing Helix test results for {WorkItems.Length} completed work item(s), with up to 16 work items in parallel.");
        var failures = new ConcurrentQueue<Exception>();
        int completedWorkItems = 0;
        long publishedResults = 0;
        await Parallel.ForEachAsync(
            WorkItems,
            new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = cancellationToken },
            async (workItem, ct) =>
            {
                try
                {
                    int testRunId = int.Parse(workItem.GetMetadata("TestRunId"), CultureInfo.InvariantCulture);
                    Log.LogMessage(MessageImportance.Low,
                        $"Downloading test-result files for '{workItem.ItemSpec}' (Azure Pipelines test run {testRunId}).");
                    var retry = new ExponentialRetry { MaxAttempts = 3 };
                    WorkItemTestResults downloaded = null;
                    Exception lastException = null;
                    bool succeeded = await retry.RunAsync(
                        async _ =>
                        {
                            try
                            {
                                downloaded = await download(workItem, ct);
                                return RetryResult.Success;
                            }
                            catch (Exception ex) when (!ct.IsCancellationRequested && TransientFailureDetector.IsTransient(ex))
                            {
                                lastException = ex;
                                Log.LogWarning($"Transient test-result download failure for '{workItem.ItemSpec}': {ex.Message}. Retrying.");
                                return RetryResult.Retry();
                            }
                        }, ct);
                    if (!succeeded)
                    {
                        throw lastException ?? new InvalidOperationException($"Result downloads did not complete for '{workItem.ItemSpec}'.");
                    }

                    Log.LogMessage(MessageImportance.Low, downloaded.TestResultFiles.Count == 0
                        ? $"No supported test-result files found for '{workItem.ItemSpec}'."
                        : $"Downloaded {downloaded.TestResultFiles.Count} test-result file(s) for '{workItem.ItemSpec}'. Parsing and aggregating results.");
                    PreparedTestResults prepared = await processor.PrepareAsync(downloaded, ct);
                    Log.LogMessage(MessageImportance.Low,
                        $"Prepared {prepared.Results.Count} top-level test result(s) from {downloaded.TestResultFiles.Count} file(s) for '{workItem.ItemSpec}'. Publishing to Azure Pipelines test run {testRunId}.");
                    long published = await publisher.PublishAsync(testRunId, downloaded, prepared, ct);
                    Interlocked.Add(ref publishedResults, published);
                    int completed = Interlocked.Increment(ref completedWorkItems);
                    string outcomes = string.Join(", ", prepared.Results.GroupBy(result => result.Result)
                        .OrderBy(group => group.Key, StringComparer.Ordinal)
                        .Select(group => $"{group.Count()} {group.Key}"));
                    Log.LogMessage(MessageImportance.High,
                        $"Work item '{downloaded.WorkItemName}' of Helix job '{downloaded.JobName}': {published} test result(s) published"
                        + (outcomes.Length == 0 ? string.Empty : $" ({outcomes})")
                        + $". Completed {completed}/{WorkItems.Length} work item(s).");
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    Log.LogError(FailureCategory.Helix, $"Failed to report test results for '{workItem.ItemSpec}': {ex.Message}");
                    failures.Enqueue(ex);
                    Interlocked.Increment(ref completedWorkItems);
                }
            });

        cancellationToken.ThrowIfCancellationRequested();
        Exception[] errors = failures.ToArray();
        Log.LogMessage(MessageImportance.High,
            $"Finished processing Helix test results: {completedWorkItems - errors.Length} succeeded, {errors.Length} failed; {publishedResults} test result(s) published from {WorkItems.Length} work item(s).");
        if (errors.Length == 1)
        {
            ExceptionDispatchInfo.Capture(errors[0]).Throw();
        }
        if (errors.Length > 1)
        {
            throw new AggregateException("Test result reporting failed for multiple work items.", errors);
        }
    }

    private string GetRequiredEnvironmentVariable(string name)
        => TaskEnvironment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Required environment variable {name} not set.");

    private sealed class BuildLogger(Microsoft.Build.Utilities.TaskLoggingHelper log) : Microsoft.Extensions.Logging.ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            string message = formatter(state, exception);
            if (exception != null)
            {
                message += Environment.NewLine + exception;
            }
            if (logLevel >= LogLevel.Error)
            {
                log.LogError(FailureCategory.Helix, message);
            }
            else if (logLevel == LogLevel.Warning)
            {
                log.LogWarning(message);
            }
            else
            {
                log.LogMessage(MessageImportance.Low, message);
            }
        }
    }
}
