// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.DotNet.Helix.Client;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.Helix.JobMonitor.Models;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Azure.Storage.Blobs.Models;

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

        public async Task<IReadOnlyList<HelixJobInfo>> GetLatestJobsAsync(CancellationToken cancellationToken)
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
                    // TODO: .Where(j => j.JobName is not in anyone's previous job property)
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

        public async Task<HelixJobInfo> ResubmitWorkItemsAsync(
            string originalJobName,
            IReadOnlyCollection<WorkItemSummary> failedWorkItems,
            CancellationToken cancellationToken)
        {
            if (failedWorkItems.Count == 0)
            {
                _logger.LogDebug("No failed work items provided for resubmission of job '{JobName}'.", originalJobName);
                return null;
            }

            // 1. Read the original job's metadata so we can clone its queue, type, source, and properties.
            JobDetails details = await RetryHelper.RetryAsync(
                () => _helixApi.Job.DetailsAsync(originalJobName),
                cancellationToken);

            if (string.IsNullOrEmpty(details.QueueId)
                || string.IsNullOrEmpty(details.Type)
                || string.IsNullOrEmpty(details.JobList))
            {
                _logger.LogWarning(
                    "Cannot resubmit job '{JobName}' because its details are missing required fields (QueueId/Type/JobList).",
                    originalJobName);
                return null;
            }

            // 2. Download the original job-list JSON; it contains the work item entries that
            //    reference the existing payload/correlation payload blobs which we want to reuse.
            string originalJobListJson;
            try
            {
                BlobClient jobListBlob = CreateBlobClient(details.JobList, resultsSas: null);
                Response<BlobDownloadResult> download = await RetryHelper.RetryAsync(
                    () => jobListBlob.DownloadContentAsync(cancellationToken),
                    cancellationToken);
                originalJobListJson = download.Value.Content.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download original job list for '{JobName}' from '{JobListUri}'.", originalJobName, details.JobList);
                return null;
            }

            // 3. Filter the entries to just the failed work items. We keep the JObject entries
            //    intact so PayloadUri / CorrelationPayloadUrisWithDestinations / Command etc. are
            //    preserved verbatim and continue to point at the original payload blobs.
            JArray originalEntries;
            try
            {
                originalEntries = JArray.Parse(originalJobListJson);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Original job list for '{JobName}' is not a valid JSON array.", originalJobName);
                return null;
            }

            var requestedSet = new HashSet<string>(failedWorkItems.Select(wi => wi.Name), StringComparer.OrdinalIgnoreCase);
            var filteredEntries = new JArray(
                originalEntries
                    .OfType<JObject>()
                    .Where(entry => entry["WorkItemId"] is JValue id
                        && id.Value is string idValue
                        && requestedSet.Contains(idValue)));

            if (filteredEntries.Count == 0)
            {
                _logger.LogWarning(
                    "Cannot resubmit job '{JobName}': none of the requested work items were found in its job list.",
                    originalJobName);
                return null;
            }

            string filteredJobListJson = filteredEntries.ToString(Formatting.Indented);

            // 4. Create a fresh storage container for the new job list and upload it.
            ContainerInformation container = await RetryHelper.RetryAsync(
                () => _helixApi.Storage.NewAsync(
                    new ContainerCreationRequest(expirationInDays: 30, desiredName: "joblists", targetQueue: details.QueueId),
                    cancellationToken),
                cancellationToken);

            string blobName = $"job-list-{Guid.NewGuid():N}.json";
            var containerUri = new Uri($"https://{container.StorageAccountName}.blob.core.windows.net/{container.ContainerName}");
            var containerClient = new BlobContainerClient(containerUri, new AzureSasCredential(container.WriteToken));
            BlobClient jobListBlobClient = containerClient.GetBlobClient(blobName);

            await RetryHelper.RetryAsync(
                () => jobListBlobClient.UploadAsync(BinaryData.FromString(filteredJobListJson), overwrite: true, cancellationToken),
                cancellationToken);

            string newJobListUri = AppendSasIfPresent(jobListBlobClient.Uri, container.ReadToken);

            // 5. Build the new job creation request, copying over Source / Properties / Creator
            //    so the resubmitted job remains discoverable (BuildId, System.StageName, TestRunName, etc.).
            var creationRequest = new JobCreationRequest(details.Type, newJobListUri, details.QueueId)
            {
                Source = details.Source,
                Creator = details.Creator,
                Properties = ConvertPropertiesToImmutableDictionary(details.Properties), // TODO: INsert originalJobName as the previous job
            };

            string idempotencyKey = Guid.NewGuid().ToString("N");
            JobCreationResult newJob = await RetryHelper.RetryAsync(
                () => _helixApi.Job.NewAsync(creationRequest, idempotencyKey, cancellationToken: cancellationToken),
                cancellationToken);

            _logger.LogInformation(
                "Resubmitted {Count} failed work item(s) from '{OriginalJobName}' as new job '{NewJobName}'.",
                filteredEntries.Count, originalJobName, newJob.Name);

            string testRunName = GetStringPropertyFromProperties(details.Properties, "TestRunName") ?? newJob.Name;
            string stageName = GetStringPropertyFromProperties(details.Properties, "System.StageName");

            return new HelixJobInfo(newJob.Name, "running", testRunName, stageName);
        }

        private static IImmutableDictionary<string, string> ConvertPropertiesToImmutableDictionary(JToken properties)
        {
            if (properties is not JObject obj)
            {
                return ImmutableDictionary<string, string>.Empty;
            }

            ImmutableDictionary<string, string>.Builder builder = ImmutableDictionary.CreateBuilder<string, string>();
            foreach (JProperty prop in obj.Properties())
            {
                if (prop.Value is null || prop.Value.Type == JTokenType.Null)
                {
                    continue;
                }

                builder[prop.Name] = prop.Value.Type == JTokenType.String
                    ? (string)prop.Value
                    : prop.Value.ToString(Formatting.None);
            }

            return builder.ToImmutable();
        }

        private static string GetStringPropertyFromProperties(JToken properties, string name)
        {
            if (properties is JObject obj && obj.TryGetValue(name, out JToken token))
            {
                string value = token?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return null;
        }

        private static string AppendSasIfPresent(Uri blobUri, string sas)
        {
            if (string.IsNullOrEmpty(sas))
            {
                return blobUri.ToString();
            }

            string trimmed = sas.StartsWith("?", StringComparison.Ordinal) ? sas.Substring(1) : sas;
            var builder = new UriBuilder(blobUri)
            {
                Query = trimmed,
            };
            return builder.Uri.ToString();
        }
    }
}
