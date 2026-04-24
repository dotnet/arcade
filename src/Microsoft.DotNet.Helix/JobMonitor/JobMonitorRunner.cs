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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    internal sealed class JobMonitorRunner : IDisposable
    {
        private readonly JobMonitorOptions _options;
        private readonly ILogger _logger;
        private readonly HttpClient _azdoClient;
        private readonly IHelixApi _helixApi;

        public JobMonitorRunner(JobMonitorOptions options, ILogger logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Directory.CreateDirectory(_options.WorkingDirectory);

            _helixApi = string.IsNullOrEmpty(_options.HelixAccessToken)
                ? ApiFactory.GetAnonymous(_options.HelixBaseUri)
                : ApiFactory.GetAuthenticated(_options.HelixBaseUri, _options.HelixAccessToken);

            _azdoClient = new HttpClient();
            string encodedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes("unused:" + _options.SystemAccessToken));
            _azdoClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedToken);
            _azdoClient.DefaultRequestHeaders.UserAgent.ParseAdd("dotnet-helix-job-monitor");
        }

        public async Task<int> RunAsync()
        {
            HashSet<string> processedRuns = await GetProcessedRunNamesAsync();

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(_options.MaximumWaitMinutes));
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            bool anyNonMonitorJobFailures = false;
            int failedHelixJobCount = 0;
            int processedHelixJobCount = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                AzureDevOpsTimelineRecord[] timelineRecords = await GetTimelineRecordsAsync();
                IImmutableList<JobSummary> jobs = await RetryAsync(
                    // TODO: "pr/public" is hardcoded but could come from the build technically
                    async () => await _helixApi.Job.ListAsync(source: $"pr/public/{_options.Organization}/{_options.RepositoryName}/refs/pull/{_options.PrNumber}/merge"),
                    cancellationToken);

                // Filter jobs to completed ones belonging to this build
                IReadOnlyCollection<JobSummary> completedJobs =
                [
                    ..jobs
                        .Where(j => ((JObject)j.Properties).TryGetValue("BuildId", out JToken buildId) && buildId?.ToString() == _options.BuildId)
                        .Where(j => j.Finished != null)
                        .OrderBy(j => j.Name, StringComparer.OrdinalIgnoreCase)
                ];

                _logger.LogInformation("{CompletedCount}/{TotalCount} Helix jobs finished", completedJobs.Count, jobs.Count);

                foreach (JobSummary job in completedJobs.Where(j => !processedRuns.Contains(j.Name)))
                {
                    bool passed = await ProcessCompletedJobAsync(job, cancellationToken);
                    processedRuns.Add(job.Name);
                    processedHelixJobCount++;
                    if (!passed)
                    {
                        failedHelixJobCount++;
                    }
                }

                anyNonMonitorJobFailures = HelixJobMonitorUtilities.HasFailedNonMonitorJobs(timelineRecords, _options.JobMonitorName);
                bool allPipelineJobsComplete = HelixJobMonitorUtilities.AreNonMonitorJobsComplete(timelineRecords, _options.JobMonitorName);
                bool allHelixJobsComplete = jobs.Count != 0 && jobs.Count == completedJobs.Count;

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

                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.PollingIntervalSeconds)), cancellationToken);
            }
        }

        public void Dispose()
        {
            _azdoClient.Dispose();
        }

        private async Task<bool> ProcessCompletedJobAsync(JobSummary helixJob, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing completed job {jobName}...", helixJob.Name);

            string testRunName = HelixJobMonitorUtilities.GetTestRunName(helixJob.Name);
            int testRunId = await StartTestRunAsync(testRunName);
            string resultsDirectory = Path.Combine(_options.WorkingDirectory, SanitizeDirName(helixJob.Name));
            Directory.CreateDirectory(resultsDirectory);

            IImmutableList<WorkItemSummary> workItems = await RetryAsync(() => _helixApi.WorkItem.ListAsync(helixJob.Name), cancellationToken);

            int failedWorkItemCount = workItems.Count(wi => wi.ExitCode != 0 || !wi.State.Equals("Finished", StringComparison.OrdinalIgnoreCase));
            bool helixJobSuccessful = failedWorkItemCount == 0;
            int sucessfulWorkItemCount = workItems.Count - failedWorkItemCount;

            try
            {
                List<WorkItemTestResults> downloadedFiles = await DownloadTestResultsAsync(helixJob.Name, workItems, resultsDirectory, cancellationToken);
                if (!await UploadDownloadedResultsAsync(downloadedFiles, testRunId, cancellationToken))
                {
                    sucessfulWorkItemCount--;
                    failedWorkItemCount++;
                    helixJobSuccessful = false;
                }
            }
            catch (Exception ex)
            {
                // TODO: Handle better here
                _logger.LogError(ex, "Failed to upload test results for job {JobName} to Azure DevOps. Test run ID was {TestRunId}.", helixJob.Name, testRunId);
                return false;
            }

            await StopTestRunAsync(testRunId, testRunName);

            _logger.LogInformation("Job '{JobName}' completed ({PassedCount} passed, {FailedCount} failed).", helixJob.Name, sucessfulWorkItemCount, failedWorkItemCount);
            return failedWorkItemCount == 0;
        }

        private async Task<HashSet<string>> GetProcessedRunNamesAsync()
        {
            // The Azure DevOps "Test Runs - List" API filters by build using the VSTFS
            // artifact URI (buildUri), not a numeric buildIds parameter. Passing buildIds
            // results in a 404 from the service.
            string buildUri = Uri.EscapeDataString($"vstfs:///Build/Build/{_options.BuildId}");
            JObject data = await SendAsync(HttpMethod.Get, $"{_options.CollectionUri}{_options.TeamProject}/_apis/test/runs?buildUri={buildUri}&api-version=7.1");
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (JObject run in (data?["value"] as JArray ?? []).Cast<JObject>())
            {
                string name = run.Value<string>("name");
                string state = run.Value<string>("state");
                if (!string.IsNullOrEmpty(name)
                    && name.StartsWith("Helix Job Monitor - ", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(state, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    processed.Add(name);
                }
            }

            return processed;
        }

        private async Task<AzureDevOpsTimelineRecord[]> GetTimelineRecordsAsync()
        {
            JObject data = await SendAsync(HttpMethod.Get, $"{_options.CollectionUri}{_options.TeamProject}/_apis/build/builds/{_options.BuildId}/timeline?api-version=7.1-preview.2");
            return data?["records"]?.ToObject<AzureDevOpsTimelineRecord[]>() ?? [];
        }

        private async Task<int> StartTestRunAsync(string testRunName)
        {
            JObject result = await SendAsync(
                HttpMethod.Post,
                $"{_options.CollectionUri}{_options.TeamProject}/_apis/test/runs?api-version=5.0",
                new JObject
                {
                    ["automated"] = true,
                    ["build"] = new JObject { ["id"] = _options.BuildId },
                    ["name"] = testRunName,
                    ["state"] = "InProgress",
                });

            return result?["id"]?.ToObject<int>() ?? 0;
        }

        private async Task StopTestRunAsync(int testRunId, string testRunName)
        {
            await SendAsync(
                new HttpMethod("PATCH"),
                $"{_options.CollectionUri}{_options.TeamProject}/_apis/test/runs/{testRunId}?api-version=5.0",
                new JObject { ["state"] = "Completed" });

            _logger.LogInformation("Stopped test run '{TestRunName}'.", testRunName);
        }

        private async Task<List<WorkItemTestResults>> DownloadTestResultsAsync(
            string jobName,
            IImmutableList<WorkItemSummary> workItems,
            string outputDirectory,
            CancellationToken cancellationToken)
        {
            List<WorkItemTestResults> downloadedFiles = [];

            JobResultsUri resultsUri = await RetryAsync(
                () => _helixApi.Job.ResultsAsync(jobName),
                cancellationToken);

            foreach (WorkItemSummary workItem in workItems)
            {
                IImmutableList<UploadedFile> availableFiles = await RetryAsync(
                    () => _helixApi.WorkItem.ListFilesAsync(workItem.Name, jobName, false),
                    cancellationToken);

                availableFiles = [.. availableFiles.Where(f => LooksLikeTestResultFile(f.Name))];

                if (availableFiles.Count == 0)
                {
                    _logger.LogInformation("Work item '{WorkItemName}' in job '{JobName}' has no test result files to download.", workItem.Name, jobName);
                    continue;
                }

                string workItemDirectory = Path.Combine(outputDirectory, SanitizeDirName(workItem.Name));
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
                        _logger.LogInformation("Downloading {FileName} for work item {WorkItemName} in job {JobName}...", file.Name, workItem.Name, jobName);

                        BlobClient blobClient = CreateBlobClient(file.Link, resultsUri.ResultsUriRSAS);
                        await blobClient.DownloadToAsync(destinationFile, cancellationToken);
                        workItemFiles.Add(destinationFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to download '{FileName}' for '{JobName}/{WorkItemName}'.", file.Name, jobName, workItem.Name);
                    }
                }

                downloadedFiles.Add(new WorkItemTestResults(jobName, workItem.Name, workItemFiles));
            }

            return downloadedFiles;
        }

        private async Task<bool> UploadDownloadedResultsAsync(List<WorkItemTestResults> testResults, int testRunId, CancellationToken cancellationToken)
        {
            var publisher = new AzureDevOpsResultPublisher(
                new AzureDevOpsReportingParameters(
                    new Uri(_options.CollectionUri, UriKind.Absolute),
                    _options.TeamProject,
                    testRunId.ToString(CultureInfo.InvariantCulture),
                    _options.SystemAccessToken),
                _logger);

            bool allTestsPassed = true;

            foreach (WorkItemTestResults workItemTestResult in testResults)
            {
                _logger.LogInformation("Publishing test results for work item '{WorkItemName}' in job '{JobName}'...", workItemTestResult.WorkItemName, workItemTestResult.JobName);
                allTestsPassed &= await publisher.UploadTestResultsAsync(
                    workItemTestResult.TestResultFiles,
                    // Metadata that will be appended to each test case
                    new
                    {
                        HelixJobId = workItemTestResult.JobName,
                        HelixWorkItemName = workItemTestResult.WorkItemName,
                    },
                cancellationToken);
            }

            return allTestsPassed;
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

                if (string.IsNullOrWhiteSpace(content))
                {
                    return [];
                }

                return JObject.Parse(content);
            }, cancellationToken);
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

    record WorkItemTestResults(string JobName, string WorkItemName, List<string> TestResultFiles);
}
