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
using Microsoft.DotNet.Helix.AzureDevOpsTestReporter;
using Microsoft.DotNet.Helix.Client;
using Microsoft.DotNet.Helix.Client.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    internal sealed class JobMonitorRunner : IDisposable
    {
        private readonly JobMonitorOptions _options;
        private readonly HttpClient _azdoClient;
        private readonly IHelixApi _helixApi;

        public JobMonitorRunner(JobMonitorOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
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
                JobStatus[] jobs = await RetryAsync(
                    // TODO async () => await _helixApi.PullRequests.ByBuildAsync(_options.Repository, _options.PrNumber, int.Parse(_options.BuildId, CultureInfo.InvariantCulture), _options.Attempt),
                    () => Task.FromResult(Array.Empty<JobStatus>()),
                    cancellationToken);

                int completedHelixJobs = jobs.Count(j => j.IsCompleted);
                int currentFailedJobs = jobs.Count(j => j.Status.Equals("failed", StringComparison.OrdinalIgnoreCase));
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {completedHelixJobs}/{jobs.Length} Helix jobs complete ({currentFailedJobs} failed). Waiting...");

                foreach (JobStatus job in jobs.Where(j => j.IsCompleted).OrderBy(j => j.JobName, StringComparer.OrdinalIgnoreCase))
                {
                    if (processedRuns.Contains(job.JobName))
                    {
                        continue;
                    }

                    JobPassFail passFail = await RetryAsync(() => _helixApi.Job.PassFailAsync(job.JobName, cancellationToken), cancellationToken);
                    bool passed = await ProcessCompletedJobAsync(job, passFail, cancellationToken);
                    processedRuns.Add(job.JobName);
                    processedHelixJobCount++;
                    if (!passed)
                    {
                        failedHelixJobCount++;
                    }
                }

                anyNonMonitorJobFailures = HelixJobMonitorUtilities.HasFailedNonMonitorJobs(timelineRecords, _options.JobMonitorName);
                bool allPipelineJobsComplete = HelixJobMonitorUtilities.AreNonMonitorJobsComplete(timelineRecords, _options.JobMonitorName);
                bool allHelixJobsComplete = jobs.Length != 0 && jobs.All(j => j.IsCompleted);

                if (allPipelineJobsComplete && allHelixJobsComplete)
                {
                    Console.WriteLine($"Final summary: processed {processedHelixJobCount} Helix job(s); {failedHelixJobCount} failed.");
                    if (anyNonMonitorJobFailures || failedHelixJobCount > 0)
                    {
                        if (anyNonMonitorJobFailures)
                        {
                            Console.Error.WriteLine("One or more non-monitor pipeline jobs failed.");
                        }

                        if (failedHelixJobCount > 0)
                        {
                            Console.Error.WriteLine($"The Helix Job Monitor detected failures in {failedHelixJobCount} Helix job(s).");
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

        private async Task<bool> ProcessCompletedJobAsync(JobStatus helixJob, JobPassFail passFail, CancellationToken cancellationToken)
        {
            string testRunName = HelixJobMonitorUtilities.GetTestRunName(helixJob.JobName);
            int testRunId = await StartTestRunAsync(testRunName);
            string resultsDirectory = Path.Combine(_options.WorkingDirectory, SanitizeDirName(helixJob.JobName));
            Directory.CreateDirectory(resultsDirectory);

            List<string> downloadedFiles = await DownloadTestResultsAsync(helixJob.JobName, passFail, resultsDirectory);

            try
            {
                await UploadDownloadedResultsAsync(downloadedFiles, testRunId, cancellationToken);
            }
            catch
            {
                // TODO: Handle here
                Console.WriteLine($"🚨 Failed to upload test results for job {helixJob.JobName} to Azure DevOps. Test run ID was {testRunId}.");
                return false;
            }

            await StopTestRunAsync(testRunId, testRunName);

            int passedCount = passFail.Passed?.Count ?? 0;
            int failedCount = passFail.Failed?.Count ?? 0;
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Job '{helixJob.JobName}' completed ({passedCount} passed, {failedCount} failed).");
            return failedCount == 0 && !string.Equals(helixJob.Status, "failed", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<HashSet<string>> GetProcessedRunNamesAsync()
        {
            JObject data = await SendAsync(HttpMethod.Get, $"{_options.CollectionUri}{_options.TeamProject}/_apis/test/runs?buildIds={_options.BuildId}&api-version=7.1-preview.1");
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

            Console.WriteLine($"Stopped test run '{testRunName}'.");
        }

        private async Task<List<WorkItemTestResults>> DownloadTestResultsAsync(
            string jobName,
            JobPassFail passFail,
            string outputDirectory,
            CancellationToken cancellationToken)
        {
            List<WorkItemTestResults> downloadedFiles = [];

            JobResultsUri resultsUri = await RetryAsync(
                () => _helixApi.Job.ResultsAsync(jobName),
                cancellationToken);

            IEnumerable<string> workItemNames = (passFail.Passed ?? [])
                .Concat(passFail.Failed ?? [])
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (string workItemName in workItemNames)
            {
                IImmutableList<UploadedFile> availableFiles = await RetryAsync(
                    () => _helixApi.WorkItem.ListFilesAsync(workItemName, jobName, false),
                    cancellationToken);

                string workItemDirectory = Path.Combine(outputDirectory, SanitizeDirName(workItemName));
                Directory.CreateDirectory(workItemDirectory);

                List<string> workItemFiles = [];
                foreach (UploadedFile file in availableFiles.Where(f => LooksLikeTestResultFile(f.Name)))
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
                        Console.WriteLine($"Downloading {file.Name} for work item {workItemName} in job {jobName}...");

                        BlobClient blobClient = CreateBlobClient(file.Link, resultsUri.ResultsUriRSAS);
                        await blobClient.DownloadToAsync(destinationFile, cancellationToken);
                        workItemFiles.Add(destinationFile);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: failed to download '{file.Name}' for '{jobName}/{workItemName}': {ex.Message}");
                    }
                }

                downloadedFiles.Add(new WorkItemTestResults(jobName, workItemName, workItemFiles));
            }

            return downloadedFiles;
        }

        private async Task UploadDownloadedResultsAsync(WorkItemTestResults testResults, int testRunId, CancellationToken cancellationToken)
        {
            var publisher = new AzureDevOpsResultPublisher(
                new AzureDevOpsReportingParameters(
                    new Uri(_options.CollectionUri, UriKind.Absolute),
                    _options.TeamProject,
                    testRunId.ToString(CultureInfo.InvariantCulture),
                    _options.SystemAccessToken));

            await publisher.UploadTestResultsAsync(
                testResults.TestResultFiles,
                new
                {
                    HelixJobId = testResults.JobName,
                    HelixWorkItemName = testResults.WorkItemName,
                },
                cancellationToken);
        }

        private async Task<JObject> SendAsync(HttpMethod method, string requestUri, JToken body = null)
        {
            return await RetryAsync(async () =>
            {
                using var request = new HttpRequestMessage(method, requestUri);
                if (body != null)
                {
                    request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                }

                using HttpResponseMessage response = await _azdoClient.SendAsync(request);
                string content = response.Content != null ? await response.Content.ReadAsStringAsync() : null;
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Request to {requestUri} failed with {(int)response.StatusCode} {response.ReasonPhrase}. {content}");
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    return [];
                }

                return JObject.Parse(content);
            });
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
