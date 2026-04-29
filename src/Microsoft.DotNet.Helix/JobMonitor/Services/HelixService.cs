// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.DotNet.Helix.Client;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.Helix.JobMonitor.Models;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    internal sealed class HelixService : IHelixService
    {
        private readonly JobMonitorOptions _options;
        private readonly ILogger _logger;
        private readonly IHelixApi _helixApi;

        public HelixService(JobMonitorOptions options, ILogger logger)
        {
            _options = options;
            _logger = logger;
            _helixApi = string.IsNullOrEmpty(options.HelixAccessToken)
                ? ApiFactory.GetAnonymous(options.HelixBaseUri)
                : ApiFactory.GetAuthenticated(options.HelixBaseUri, options.HelixAccessToken);
        }

        public async Task<IReadOnlyList<HelixJobInfo>> GetJobsAsync(CancellationToken cancellationToken)
        {
            // Build the Helix source filter. For PR builds, use the PR merge ref.
            // For CI builds without a PR, use the branch-based source.
            string source = _options.PrNumber.HasValue
                ? $"pr/public/{_options.Organization}/{_options.RepositoryName}/refs/pull/{_options.PrNumber}/merge"
                : $"official/public/{_options.Organization}/{_options.RepositoryName}";

            IImmutableList<JobSummary> jobs = await RetryHelper.RetryAsync(
                async () => await _helixApi.Job.ListAsync(source: source),
                cancellationToken);

            return
            [
                ..jobs
                    .Where(j => ((JObject)j.Properties).TryGetValue("BuildId", out JToken buildId) && buildId?.ToString() == _options.BuildId)
                    .Select(j => new HelixJobInfo(
                        j.Name,
                        j.Finished != null ? "finished" : "running",
                        GetTestRunNameFromJob(j),
                        GetStringPropertyFromJob(j, "System.StageName")))
             ];
        }

        public async Task<IReadOnlyList<WorkItemTestResults>> DownloadTestResultsAsync(
            string jobName,
            IReadOnlyCollection<string> workItemNames,
            CancellationToken cancellationToken)
        {
            List<WorkItemTestResults> downloadedFiles = [];
            string outputDirectory = Path.Combine(_options.WorkingDirectory, SanitizeDirName(jobName));
            Directory.CreateDirectory(outputDirectory);

            JobResultsUri resultsUri = await RetryHelper.RetryAsync(() => _helixApi.Job.ResultsAsync(jobName), cancellationToken);

            foreach (string workItemName in workItemNames)
            {
                IImmutableList<UploadedFile> availableFiles = await RetryHelper.RetryAsync(
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

        private static string GetStringPropertyFromJob(JobSummary helixJob, string propertyName)
        {
            if (helixJob.Properties is JObject properties
                && properties.TryGetValue(propertyName, out JToken token))
            {
                string value = token?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return null;
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

        public async Task<IReadOnlyCollection<WorkItemSummary>> ListWorkItemsAsync(
            string jobName,
            CancellationToken cancellationToken)
        {
            return await RetryHelper.RetryAsync(() => _helixApi.WorkItem.ListAsync(jobName), cancellationToken);
        }

        public Task<HelixJobInfo> ResubmitFailedWorkItemsAsync(
            string originalJobName,
            IReadOnlyCollection<string> failedWorkItemNames,
            CancellationToken cancellationToken)
        {
            // TODO: Implement real Helix resubmission via the Helix API.
            // This should:
            // 1. Get the original job's details (queue, correlation payloads, properties)
            // 2. Create a new job with the same configuration but only the failed work items
            // 3. Preserve BuildId, StageName, and other discovery properties
            throw new NotImplementedException("Real Helix resubmission is not yet implemented.");
        }
    }
}
