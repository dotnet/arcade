// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
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
        var processor = new TestResultProcessor(TestResultAttachmentMode.Failed, useFullyQualifiedTestName: false, logger, metrics, failOnParseError: true);
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

        await Parallel.ForEachAsync(
            WorkItems,
            new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = cancellationToken },
            async (workItem, ct) =>
            {
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

                PreparedTestResults prepared = await processor.PrepareAsync(downloaded, ct);
                int testRunId = int.Parse(workItem.GetMetadata("TestRunId"), CultureInfo.InvariantCulture);
                long published = await publisher.PublishAsync(testRunId, downloaded, prepared, ct);
                Log.LogMessage(MessageImportance.High,
                    $"Published {published} test result(s) from '{workItem.ItemSpec}' to Azure Pipelines test run {testRunId}.");
            });
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
                log.LogMessage(logLevel >= LogLevel.Information ? MessageImportance.Normal : MessageImportance.Low, message);
            }
        }
    }
}
