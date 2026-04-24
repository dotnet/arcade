// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher.Model;
using Microsoft.DotNet.Helix.Client;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.Helix.JobMonitor.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    internal sealed class JobMonitorRunner : IJobMonitorRunner, IDisposable
    {
        private readonly JobMonitorOptions _options;
        private readonly ILogger _logger;
        private readonly IAzureDevOpsService _azdo;
        private readonly IHelixService _helix;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayFunc;

        /// <summary>
        /// Constructor for production use with real services.
        /// </summary>
        public JobMonitorRunner(JobMonitorOptions options, ILogger logger)
            : this(options, logger,
                   CreateRealAzureDevOpsService(options, logger),
                   CreateRealHelixService(options, logger),
                   null)
        {
        }

        /// <summary>
        /// Constructor for testing with injected services.
        /// </summary>
        internal JobMonitorRunner(
            JobMonitorOptions options,
            ILogger logger,
            IAzureDevOpsService azdo,
            IHelixService helix,
            Func<TimeSpan, CancellationToken, Task> delayFunc)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _azdo = azdo ?? throw new ArgumentNullException(nameof(azdo));
            _helix = helix ?? throw new ArgumentNullException(nameof(helix));
            _delayFunc = delayFunc ?? Task.Delay;
            Directory.CreateDirectory(_options.WorkingDirectory);
        }

        public Task<int> RunAsync(CancellationToken cancellationToken)
        {
            return RunCoreAsync(cancellationToken);
        }

        public async Task<int> RunAsync()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(_options.MaximumWaitMinutes));
            return await RunCoreAsync(cancellationTokenSource.Token);
        }

        private async Task<int> RunCoreAsync(CancellationToken cancellationToken)
        {
            IReadOnlySet<string> alreadyProcessed = await _azdo.GetProcessedHelixJobNamesAsync(cancellationToken);
            var processedHelixJobs = new HashSet<string>(alreadyProcessed, StringComparer.OrdinalIgnoreCase);

            bool anyNonMonitorJobFailures = false;
            int failedHelixJobCount = 0;
            int processedHelixJobCount = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IReadOnlyList<AzureDevOpsTimelineRecord> timelineRecords = await _azdo.GetTimelineRecordsAsync(cancellationToken);
                IReadOnlyList<HelixJobInfo> jobs = await _helix.GetJobsAsync(cancellationToken);

                IReadOnlyCollection<HelixJobInfo> completedJobs = jobs
                    .Where(j => j.IsCompleted)
                    .OrderBy(j => j.JobName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _logger.LogInformation("{CompletedCount}/{TotalCount} Helix jobs finished", completedJobs.Count, jobs.Count);

                foreach (HelixJobInfo job in completedJobs.Where(j => !processedHelixJobs.Contains(j.JobName)))
                {
                    bool passed = await ProcessCompletedJobAsync(job, processedHelixJobs, cancellationToken);
                    processedHelixJobCount++;
                    if (!passed)
                    {
                        failedHelixJobCount++;
                    }
                }

                anyNonMonitorJobFailures = HelixJobMonitorUtilities.HasFailedNonMonitorJobs(timelineRecords, _options.JobMonitorName);
                bool allPipelineJobsComplete = HelixJobMonitorUtilities.AreNonMonitorJobsComplete(timelineRecords, _options.JobMonitorName);
                bool allHelixJobsComplete = jobs.Count == 0 || jobs.All(j => j.IsCompleted);

                if (allPipelineJobsComplete && allHelixJobsComplete)
                {
                    _logger.LogInformation("Final summary: processed {ProcessedCount} Helix job(s); {FailedCount} failed.", processedHelixJobCount, failedHelixJobCount);
                    if (anyNonMonitorJobFailures || failedHelixJobCount > 0)
                    {
                        if (anyNonMonitorJobFailures)
                        {
                            _logger.LogError("One or more non-monitor pipeline jobs failed.");
                        }

                        if (failedHelixJobCount > 0)
                        {
                            _logger.LogError("The Helix Job Monitor detected failures in {FailedCount} Helix job(s).", failedHelixJobCount);
                        }

                        return 1;
                    }

                    return 0;
                }

                // If all pipeline jobs are dead and Helix jobs are still running,
                // those jobs are orphaned — no point waiting.
                if (allPipelineJobsComplete && anyNonMonitorJobFailures && !allHelixJobsComplete)
                {
                    _logger.LogError("All non-monitor pipeline jobs failed/canceled while Helix jobs are still running. Exiting.");
                    return 1;
                }

                await _delayFunc(TimeSpan.FromSeconds(Math.Max(5, _options.PollingIntervalSeconds)), cancellationToken);
            }
        }

        private async Task<bool> ProcessCompletedJobAsync(
            HelixJobInfo job,
            HashSet<string> processedHelixJobs,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing completed job {jobName}...", job.JobName);

            string testRunName = job.TestRunName ?? job.JobName;
            int testRunId = await _azdo.CreateTestRunAsync(testRunName, job.JobName, cancellationToken);

            try
            {
                HelixJobPassFail passFail = await _helix.GetJobPassFailAsync(job.JobName, cancellationToken);
                IReadOnlyList<WorkItemTestResults> downloadedResults = await _helix.DownloadTestResultsAsync(
                    job.JobName, passFail, cancellationToken);

                bool allTestsPassed = await _azdo.UploadTestResultsAsync(testRunId, downloadedResults, cancellationToken);
                await _azdo.CompleteTestRunAsync(testRunId, cancellationToken);

                processedHelixJobs.Add(job.JobName);

                _logger.LogInformation("Job '{JobName}' completed ({PassedCount} passed, {FailedCount} failed).",
                    job.JobName, passFail.PassedWorkItems.Count, passFail.FailedWorkItems.Count);

                return !passFail.HasFailures && allTestsPassed
                    && !job.Status.Equals("failed", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process job '{JobName}'. Test run ID was {TestRunId}.", job.JobName, testRunId);
                // Don't add to processedHelixJobs — allows retry to pick it up.
                return false;
            }
        }

        public void Dispose()
        {
            // Real services handle their own cleanup via the azdo/helix service implementations
        }

        // -----------------------------------------------------------------
        // Factory methods for production service implementations
        // -----------------------------------------------------------------

        private static IAzureDevOpsService CreateRealAzureDevOpsService(JobMonitorOptions options, ILogger logger)
            => new RealAzureDevOpsService(options, logger);

        private static IHelixService CreateRealHelixService(JobMonitorOptions options, ILogger logger)
            => new RealHelixService(options, logger);

        // -----------------------------------------------------------------
        // Real AzDO service implementation (extracted from original runner)
        // -----------------------------------------------------------------

        private sealed class RealAzureDevOpsService : IAzureDevOpsService, IDisposable
        {
            private const string MonitoredJobTagPrefix = "MonitoredJob:";
            private readonly JobMonitorOptions _options;
            private readonly ILogger _logger;
            private readonly HttpClient _azdoClient;

            public RealAzureDevOpsService(JobMonitorOptions options, ILogger logger)
            {
                _options = options;
                _logger = logger;
                _azdoClient = new HttpClient();
                string encodedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes("unused:" + options.SystemAccessToken));
                _azdoClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedToken);
                _azdoClient.DefaultRequestHeaders.UserAgent.ParseAdd("dotnet-helix-job-monitor");
            }

            public async Task<IReadOnlyList<AzureDevOpsTimelineRecord>> GetTimelineRecordsAsync(CancellationToken cancellationToken)
            {
                JObject data = await SendAsync(HttpMethod.Get, $"{_options.CollectionUri}{_options.TeamProject}/_apis/build/builds/{_options.BuildId}/timeline?api-version=7.1-preview.2", cancellationToken: cancellationToken);
                return data?["records"]?.ToObject<AzureDevOpsTimelineRecord[]>() ?? [];
            }

            public async Task<IReadOnlySet<string>> GetProcessedHelixJobNamesAsync(CancellationToken cancellationToken)
            {
                string buildUri = Uri.EscapeDataString($"vstfs:///Build/Build/{_options.BuildId}");
                JObject data = await SendAsync(HttpMethod.Get, $"{_options.CollectionUri}{_options.TeamProject}/_apis/test/runs?buildUri={buildUri}&api-version=7.1", cancellationToken: cancellationToken);
                var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (JObject run in (data?["value"] as JArray ?? []).Cast<JObject>())
                {
                    int? runId = run.Value<int?>("id");
                    string state = run.Value<string>("state");
                    if (runId == null || !string.Equals(state, "Completed", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string helixJobName = await GetMonitoredHelixJobNameAsync(runId.Value, cancellationToken);
                    if (!string.IsNullOrEmpty(helixJobName))
                    {
                        processed.Add(helixJobName);
                    }
                }

                return processed;
            }

            private async Task<string> GetMonitoredHelixJobNameAsync(int testRunId, CancellationToken cancellationToken)
            {
                JObject run = await SendAsync(HttpMethod.Get, $"{_options.CollectionUri}{_options.TeamProject}/_apis/test/runs/{testRunId}?api-version=7.1", cancellationToken: cancellationToken);
                if (run?["tags"] is not JArray tags)
                {
                    return null;
                }

                foreach (JToken tag in tags)
                {
                    string tagName = tag?.Value<string>("name");
                    if (!string.IsNullOrEmpty(tagName) && tagName.StartsWith(MonitoredJobTagPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return tagName.Substring(MonitoredJobTagPrefix.Length);
                    }
                }

                return null;
            }

            public async Task<int> CreateTestRunAsync(string name, string helixJobName, CancellationToken cancellationToken)
            {
                JObject result = await SendAsync(HttpMethod.Post,
                    $"{_options.CollectionUri}{_options.TeamProject}/_apis/test/runs?api-version=5.0",
                    new JObject
                    {
                        ["automated"] = true,
                        ["build"] = new JObject { ["id"] = _options.BuildId },
                        ["name"] = name,
                        ["state"] = "InProgress",
                        ["tags"] = new JArray { new JObject { ["name"] = MonitoredJobTagPrefix + helixJobName } },
                    },
                    cancellationToken: cancellationToken);
                return result?["id"]?.ToObject<int>() ?? 0;
            }

            public async Task CompleteTestRunAsync(int testRunId, CancellationToken cancellationToken)
            {
                await SendAsync(new HttpMethod("PATCH"),
                    $"{_options.CollectionUri}{_options.TeamProject}/_apis/test/runs/{testRunId}?api-version=5.0",
                    new JObject { ["state"] = "Completed" },
                    cancellationToken: cancellationToken);
            }

            public async Task<bool> UploadTestResultsAsync(int testRunId, IReadOnlyList<WorkItemTestResults> results, CancellationToken cancellationToken)
            {
                var publisher = new AzureDevOpsResultPublisher(
                    new AzureDevOpsReportingParameters(
                        new Uri(_options.CollectionUri, UriKind.Absolute),
                        _options.TeamProject,
                        testRunId.ToString(CultureInfo.InvariantCulture),
                        _options.SystemAccessToken),
                    _logger);

                bool allPassed = true;
                foreach (WorkItemTestResults workItem in results)
                {
                    _logger.LogInformation("Publishing test results for work item '{WorkItemName}' in job '{JobName}'...", workItem.WorkItemName, workItem.JobName);
                    allPassed &= await publisher.UploadTestResultsAsync(workItem.TestResultFiles,
                        new { HelixJobId = workItem.JobName, HelixWorkItemName = workItem.WorkItemName },
                        cancellationToken);
                }

                return allPassed;
            }

            private async Task<JObject> SendAsync(HttpMethod method, string requestUri, JToken body = null, CancellationToken cancellationToken = default)
            {
                return await RetryAsync(async () =>
                {
                    using var request = new HttpRequestMessage(method, requestUri);
                    if (body != null)
                    {
                        request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                    }

                    using HttpResponseMessage response = await _azdoClient.SendAsync(request, cancellationToken);
                    string content = response.Content != null ? await response.Content.ReadAsStringAsync(cancellationToken) : null;
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException($"Request to {requestUri} failed with {(int)response.StatusCode} {response.ReasonPhrase}. {content}");
                    }

                    return string.IsNullOrWhiteSpace(content) ? [] : JObject.Parse(content);
                }, cancellationToken);
            }

            public void Dispose() => _azdoClient.Dispose();
        }

        // -----------------------------------------------------------------
        // Real Helix service implementation (extracted from original runner)
        // -----------------------------------------------------------------

        private sealed class RealHelixService : IHelixService
        {
            private readonly JobMonitorOptions _options;
            private readonly ILogger _logger;
            private readonly IHelixApi _helixApi;

            public RealHelixService(JobMonitorOptions options, ILogger logger)
            {
                _options = options;
                _logger = logger;
                _helixApi = string.IsNullOrEmpty(options.HelixAccessToken)
                    ? ApiFactory.GetAnonymous(options.HelixBaseUri)
                    : ApiFactory.GetAuthenticated(options.HelixBaseUri, options.HelixAccessToken);
            }

            public async Task<IReadOnlyList<HelixJobInfo>> GetJobsAsync(CancellationToken cancellationToken)
            {
                IImmutableList<JobSummary> jobs = await RetryAsync(
                    async () => await _helixApi.Job.ListAsync(
                        source: $"pr/public/{_options.Organization}/{_options.RepositoryName}/refs/pull/{_options.PrNumber}/merge"),
                    cancellationToken);

                return jobs
                    .Where(j => ((JObject)j.Properties).TryGetValue("BuildId", out JToken buildId) && buildId?.ToString() == _options.BuildId)
                    .Select(j => new HelixJobInfo(
                        j.Name,
                        j.Finished != null ? "finished" : "running",
                        GetTestRunNameFromJob(j)))
                    .ToList();
            }

            public async Task<HelixJobPassFail> GetJobPassFailAsync(string jobName, CancellationToken cancellationToken)
            {
                IImmutableList<WorkItemSummary> workItems = await RetryAsync(
                    () => _helixApi.WorkItem.ListAsync(jobName),
                    cancellationToken);

                var passed = new List<string>();
                var failed = new List<string>();

                foreach (WorkItemSummary wi in workItems)
                {
                    if (wi.ExitCode != 0 || !wi.State.Equals("Finished", StringComparison.OrdinalIgnoreCase))
                    {
                        failed.Add(wi.Name);
                    }
                    else
                    {
                        passed.Add(wi.Name);
                    }
                }

                return new HelixJobPassFail(passed, failed);
            }

            public async Task<IReadOnlyList<WorkItemTestResults>> DownloadTestResultsAsync(
                string jobName,
                HelixJobPassFail passFail,
                CancellationToken cancellationToken)
            {
                List<WorkItemTestResults> downloadedFiles = [];
                string outputDirectory = Path.Combine(_options.WorkingDirectory, SanitizeDirName(jobName));
                Directory.CreateDirectory(outputDirectory);

                JobResultsUri resultsUri = await RetryAsync(() => _helixApi.Job.ResultsAsync(jobName), cancellationToken);

                IEnumerable<string> workItemNames = passFail.PassedWorkItems
                    .Concat(passFail.FailedWorkItems)
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (string workItemName in workItemNames)
                {
                    IImmutableList<UploadedFile> availableFiles = await RetryAsync(
                        () => _helixApi.WorkItem.ListFilesAsync(workItemName, jobName, false),
                        cancellationToken);

                    availableFiles = [.. availableFiles.Where(f => LooksLikeTestResultFile(f.Name))];
                    if (availableFiles.Count == 0)
                    {
                        continue;
                    }

                    string workItemDirectory = Path.Combine(outputDirectory, SanitizeDirName(workItemName));
                    Directory.CreateDirectory(workItemDirectory);

                    List<string> workItemFiles = [];
                    foreach (UploadedFile file in availableFiles)
                    {
                        string relativePath = file.Name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                        string destinationFile = Path.Combine(workItemDirectory, relativePath);
                        string directory = Path.GetDirectoryName(destinationFile);
                        if (!string.IsNullOrEmpty(directory))
                        {
                            Directory.CreateDirectory(directory);
                        }

                        try
                        {
                            BlobClient blobClient = CreateBlobClient(file.Link, resultsUri.ResultsUriRSAS);
                            await blobClient.DownloadToAsync(destinationFile, cancellationToken);
                            workItemFiles.Add(destinationFile);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to download '{FileName}' for '{JobName}/{WorkItemName}'.", file.Name, jobName, workItemName);
                        }
                    }

                    downloadedFiles.Add(new WorkItemTestResults(jobName, workItemName, workItemFiles));
                }

                return downloadedFiles;
            }

            private static string GetTestRunNameFromJob(JobSummary helixJob)
            {
                if (helixJob.Properties is JObject properties
                    && properties.TryGetValue("TestRunName", out JToken testRunName))
                {
                    string value = testRunName?.ToString();
                    if (!string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }

                return helixJob.Name;
            }

            private static BlobClient CreateBlobClient(string fileLink, string resultsSas)
            {
                var options = new BlobClientOptions();
                options.Retry.NetworkTimeout = TimeSpan.FromMinutes(5);
                if (string.IsNullOrEmpty(resultsSas))
                {
                    return new BlobClient(new Uri(fileLink), options);
                }

                string strippedUri = fileLink.Contains('?') ? fileLink.Substring(0, fileLink.LastIndexOf('?', StringComparison.Ordinal)) : fileLink;
                return new BlobClient(new Uri(strippedUri), new AzureSasCredential(resultsSas), options);
            }

            private static bool LooksLikeTestResultFile(string path)
                => LocalTestResultsReader.LooksLikeTestResultFile(path);

            private static string SanitizeDirName(string value)
            {
                foreach (char invalidChar in Path.GetInvalidFileNameChars())
                {
                    value = value.Replace(invalidChar, '-');
                }

                return value;
            }
        }

        // -----------------------------------------------------------------
        // Shared retry helper
        // -----------------------------------------------------------------

        private static async Task<T> RetryAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
        {
            Exception last = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return await action();
                }
                catch (Exception ex) when (attempt < 4)
                {
                    last = ex;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)), cancellationToken);
                }
            }

            throw last ?? new InvalidOperationException("Retry failed without capturing an exception.");
        }
    }

    public record WorkItemTestResults(string JobName, string WorkItemName, List<string> TestResultFiles);
}
