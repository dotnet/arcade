// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
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
            string encodedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes("unused:" + _options.AccessToken));
            _azdoClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedToken);
            _azdoClient.DefaultRequestHeaders.UserAgent.ParseAdd("dotnet-helix-job-monitor");
        }

        public async Task<int> RunAsync()
        {
            HashSet<string> processedRuns = await GetProcessedRunNamesAsync().ConfigureAwait(false);
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, _options.MaximumWaitMinutes));
            bool anyNonMonitorJobFailures = false;
            int failedHelixJobCount = 0;
            int processedHelixJobCount = 0;

            while (DateTimeOffset.UtcNow < deadline)
            {
                AzureDevOpsTimelineRecord[] timelineRecords = await GetTimelineRecordsAsync().ConfigureAwait(false);
                JobStatus[] jobs = await RetryAsync(
                    () => Task.FromResult(Array.Empty<JobStatus>())
                        /*TODO _helixApi.PullRequests.ByBuildAsync(_options.Repository, _options.PrNumber, int.Parse(_options.BuildId, CultureInfo.InvariantCulture), _options.Attempt)*/)
                    .ConfigureAwait(false);

                int completedHelixJobs = jobs.Count(j => j.IsCompleted);
                int currentFailedJobs = jobs.Count(j => j.Status.Equals("failed", StringComparison.OrdinalIgnoreCase));
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {completedHelixJobs}/{jobs.Length} Helix jobs complete ({currentFailedJobs} failed). Waiting...");

                foreach (JobStatus job in jobs.Where(j => j.IsCompleted).OrderBy(j => j.JobName, StringComparer.OrdinalIgnoreCase))
                {
                    if (processedRuns.Contains(job.JobName))
                    {
                        continue;
                    }

                    JobPassFail passFail = await RetryAsync(() => _helixApi.Job.PassFailAsync(job.JobName)).ConfigureAwait(false);
                    bool passed = await ProcessCompletedJobAsync(job, passFail).ConfigureAwait(false);
                    processedRuns.Add(job.JobName);
                    processedHelixJobCount++;
                    if (!passed)
                    {
                        failedHelixJobCount++;
                    }
                }

                anyNonMonitorJobFailures = HelixJobMonitorUtilities.HasFailedNonMonitorJobs(timelineRecords, _options.JobMonitorName);
                bool allPipelineJobsComplete = HelixJobMonitorUtilities.AreNonMonitorJobsComplete(timelineRecords, _options.JobMonitorName);
                bool allHelixJobsComplete = jobs.Any() && jobs.All(j => j.IsCompleted);

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

                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.PollingIntervalSeconds))).ConfigureAwait(false);
            }

            Console.Error.WriteLine($"The Helix Job Monitor timed out after {_options.MaximumWaitMinutes} minute(s).");
            return 1;
        }

        public void Dispose()
        {
            _azdoClient.Dispose();
        }

        private async Task<bool> ProcessCompletedJobAsync(JobStatus helixJob, JobPassFail passFail)
        {
            string testRunName = HelixJobMonitorUtilities.GetTestRunName(helixJob.JobName);
            int testRunId = await StartTestRunAsync(testRunName).ConfigureAwait(false);
            string resultsDirectory = Path.Combine(_options.WorkingDirectory, MakeSafeDirectoryName(helixJob.JobName));
            Directory.CreateDirectory(resultsDirectory);

            int downloadedFiles = await DownloadTestResultsAsync(helixJob.JobName, passFail, resultsDirectory).ConfigureAwait(false);
            bool reporterRan = downloadedFiles > 0 && await TryRunPythonReporterAsync(resultsDirectory, testRunId).ConfigureAwait(false);
            if (!reporterRan)
            {
                await CreateFallbackResultsAsync(testRunId, helixJob.JobName, passFail).ConfigureAwait(false);
            }

            await StopTestRunAsync(testRunId, testRunName).ConfigureAwait(false);

            int passedCount = passFail.Passed?.Count ?? 0;
            int failedCount = passFail.Failed?.Count ?? 0;
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Job '{helixJob.JobName}' completed ({passedCount} passed, {failedCount} failed).");
            return failedCount == 0 && !string.Equals(helixJob.Status, "failed", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<HashSet<string>> GetProcessedRunNamesAsync()
        {
            JObject data = await SendAsync(HttpMethod.Get, $"{_options.CollectionUri}{_options.TeamProject}/_apis/test/runs?buildIds={_options.BuildId}&api-version=7.1-preview.1").ConfigureAwait(false);
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (JObject run in data?["value"] as JArray ?? [])
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
            JObject data = await SendAsync(HttpMethod.Get, $"{_options.CollectionUri}{_options.TeamProject}/_apis/build/builds/{_options.BuildId}/timeline?api-version=7.1-preview.2").ConfigureAwait(false);
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
                }).ConfigureAwait(false);

            return result?["id"]?.ToObject<int>() ?? 0;
        }

        private async Task StopTestRunAsync(int testRunId, string testRunName)
        {
            await SendAsync(
                new HttpMethod("PATCH"),
                $"{_options.CollectionUri}{_options.TeamProject}/_apis/test/runs/{testRunId}?api-version=5.0",
                new JObject { ["state"] = "Completed" }).ConfigureAwait(false);

            Console.WriteLine($"Stopped test run '{testRunName}'.");
        }

        private async Task CreateFallbackResultsAsync(int testRunId, string jobName, JobPassFail passFail)
        {
            foreach (string workItemName in passFail.Passed ?? ImmutableList<string>.Empty)
            {
                await CreateFallbackResultAsync(testRunId, jobName, workItemName, failed: false).ConfigureAwait(false);
            }

            foreach (string workItemName in passFail.Failed ?? ImmutableList<string>.Empty)
            {
                await CreateFallbackResultAsync(testRunId, jobName, workItemName, failed: true).ConfigureAwait(false);
            }
        }

        private async Task CreateFallbackResultAsync(int testRunId, string jobName, string workItemName, bool failed)
        {
            string cleanName = HelixJobMonitorUtilities.CleanWorkItemName(workItemName);
            await SendAsync(
                HttpMethod.Post,
                $"{_options.CollectionUri}{_options.TeamProject}/_apis/test/Runs/{testRunId}/results?api-version=5.1-preview.6",
                new JArray
                {
                    new JObject
                    {
                        ["automatedTestName"] = $"{cleanName}.WorkItemExecution",
                        ["automatedTestStorage"] = cleanName,
                        ["testCaseTitle"] = $"{cleanName} Work Item",
                        ["outcome"] = failed ? "Failed" : "Passed",
                        ["state"] = "Completed",
                        ["errorMessage"] = failed ? "The Helix work item failed. See the Helix logs for more details." : null,
                        ["durationInMs"] = 60 * 1000,
                        ["comment"] = new JObject
                        {
                            ["HelixJobId"] = jobName,
                            ["HelixWorkItemName"] = cleanName,
                        }.ToString(),
                    }
                }).ConfigureAwait(false);
        }

        private async Task<int> DownloadTestResultsAsync(string jobName, JobPassFail passFail, string outputDirectory)
        {
            int count = 0;
            JobResultsUri resultsUri = await RetryAsync(() => _helixApi.Job.ResultsAsync(jobName)).ConfigureAwait(false);
            IEnumerable<string> workItemNames = (passFail.Passed ?? ImmutableList<string>.Empty).Concat(passFail.Failed ?? ImmutableList<string>.Empty);

            foreach (string workItemName in workItemNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var availableFiles = await RetryAsync(() => _helixApi.WorkItem.ListFilesAsync(workItemName, jobName, false)).ConfigureAwait(false);
                string workItemDirectory = Path.Combine(outputDirectory, MakeSafeDirectoryName(workItemName));
                Directory.CreateDirectory(workItemDirectory);

                foreach (var file in availableFiles.Where(f => LooksLikeTestResultFile(f.Name)))
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
                        await blobClient.DownloadToAsync(destinationFile).ConfigureAwait(false);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: failed to download '{file.Name}' for '{jobName}/{workItemName}': {ex.Message}");
                    }
                }
            }

            return count;
        }

        private async Task<bool> TryRunPythonReporterAsync(string workingDirectory, int testRunId)
        {
            string scriptPath = Path.Combine(AppContext.BaseDirectory, "reporter", "run.py");
            if (!File.Exists(scriptPath))
            {
                Console.WriteLine($"Warning: reporter script was not found at '{scriptPath}'. Falling back to synthetic work-item results.");
                return false;
            }

            foreach ((string fileName, string prefixArguments) in GetPythonCandidates())
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = $"{prefixArguments}\"{scriptPath}\" \"{_options.CollectionUri}\" \"{_options.TeamProject}\" \"{testRunId.ToString(CultureInfo.InvariantCulture)}\" \"{_options.AccessToken}\"",
                        WorkingDirectory = workingDirectory,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    };

                    using Process process = Process.Start(psi);
                    if (process == null)
                    {
                        continue;
                    }

                    string stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                    string stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    await process.WaitForExitAsync().ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(stdout))
                    {
                        Console.WriteLine(stdout);
                    }

                    if (!string.IsNullOrWhiteSpace(stderr))
                    {
                        Console.WriteLine(stderr);
                    }

                    if (process.ExitCode == 0)
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: failed to invoke Python reporter via '{fileName}': {ex.Message}");
                }
            }

            return false;
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

                using HttpResponseMessage response = await _azdoClient.SendAsync(request).ConfigureAwait(false);
                string content = response.Content != null ? await response.Content.ReadAsStringAsync().ConfigureAwait(false) : null;
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Request to {requestUri} failed with {(int)response.StatusCode} {response.ReasonPhrase}. {content}");
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    return new JObject();
                }

                return JObject.Parse(content);
            }).ConfigureAwait(false);
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
        {
            string fileName = Path.GetFileName(path);
            return fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                   && (fileName.Contains("testresults", StringComparison.OrdinalIgnoreCase)
                       || fileName.Contains("test-results", StringComparison.OrdinalIgnoreCase));
        }

        private static string MakeSafeDirectoryName(string value)
        {
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalidChar, '-');
            }

            return value;
        }

        private static IEnumerable<(string fileName, string prefixArguments)> GetPythonCandidates()
        {
            if (OperatingSystem.IsWindows())
            {
                yield return ("py", "-3 ");
            }

            yield return ("python3", string.Empty);
            yield return ("python", string.Empty);
        }

        private static async Task<T> RetryAsync<T>(Func<Task<T>> action)
        {
            Exception last = null;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    return await action().ConfigureAwait(false);
                }
                catch (Exception ex) when (attempt < 4)
                {
                    last = ex;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1))).ConfigureAwait(false);
                }
            }

            throw last ?? new InvalidOperationException("Retry failed without capturing an exception.");
        }
    }
}
