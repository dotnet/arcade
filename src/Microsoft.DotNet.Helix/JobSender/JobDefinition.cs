using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.Rest;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client
{
    internal class JobDefinition : IJobDefinitionWithSource,
        IJobDefinitionWithType,
        IJobDefinitionWithBuild,
        IJobDefinitionWithTargetQueue,
        IJobDefinition
    {
        private readonly Dictionary<string, string> _properties;
        private readonly List<WorkItemDefinition> _workItems;
        private readonly string _jobStartIdentifier;
        private const int MAX_RETRIES = 15;

        public JobDefinition(IJob jobApi)
        {
            _workItems = new List<WorkItemDefinition>();
            WorkItems = _workItems.AsReadOnly();
            _properties = new Dictionary<string, string>();
            Properties = new ReadOnlyDictionary<string, string>(_properties);
            JobApi = jobApi;
            _jobStartIdentifier = Guid.NewGuid().ToString("N");
            HelixApi = ((IServiceOperations<HelixApi>)JobApi).Client;
        }

        public IHelixApi HelixApi { get; }
        public IJob JobApi { get; }

        public IReadOnlyList<IWorkItemDefinition> WorkItems { get; }
        public IReadOnlyDictionary<string, string> Properties { get; }
        public string Source { get; private set; }
        public string Type { get; private set; }
        public string Build { get; private set; }
        public string TargetQueueId { get; private set; }
        public string Creator { get; private set; }
        public IList<IPayload> CorrelationPayloads { get; } = new List<IPayload>();
        public int? MaxRetryCount { get; private set; }
        public string StorageAccountConnectionString { get; private set; }
        public string TargetContainerName { get; set; } = DefaultContainerName;
        public static string DefaultContainerName => $"helix-job-{Guid.NewGuid()}";

        public IWorkItemDefinitionWithCommand DefineWorkItem(string workItemName)
        {
            return new WorkItemDefinition(this, workItemName);
        }

        public IJobDefinition WithCorrelationPayloadUris(params Uri[] payloadUris)
        {
            foreach (Uri uri in payloadUris)
            {
                CorrelationPayloads.Add(new UriPayload(uri));
            }
            return this;
        }

        public IJobDefinition WithCorrelationPayloadDirectory(string directory)
        {
            return WithCorrelationPayloadDirectory(directory, false);
        }

        public IJobDefinition WithCorrelationPayloadDirectory(string directory, bool includeDirectoryName)
        {
            string archiveEntryPrefix = null;
            if (includeDirectoryName)
            {
                archiveEntryPrefix = new DirectoryInfo(directory).Name;
            }
            return WithCorrelationPayloadDirectory(directory, archiveEntryPrefix);
        }

        public IJobDefinition WithCorrelationPayloadDirectory(string directory, string archiveEntryPrefix)
        {
            CorrelationPayloads.Add(new DirectoryPayload(directory, archiveEntryPrefix));
            return this;
        }

        public IJobDefinition WithCorrelationPayloadFiles(params string[] files)
        {
            CorrelationPayloads.Add(new AdhocPayload(files));
            return this;
        }

        public IJobDefinition WithCorrelationPayloadArchive(string archive)
        {
            CorrelationPayloads.Add(new ArchivePayload(archive));
            return this;
        }

        public IJobDefinition WithProperty(string key, string value)
        {
            _properties[key] = value;
            return this;
        }

        public IJobDefinition WithCreator(string creator)
        {
            Creator = creator;
            return this;
        }

        public IJobDefinition WithContainerName(string targetContainerName)
        {
            TargetContainerName = targetContainerName;
            return this;
        }

        public IJobDefinition WithStorageAccountConnectionString(string accountConnectionString)
        {
            StorageAccountConnectionString = accountConnectionString;
            return this;
        }

        public async Task<ISentJob> SendAsync(Action<LogLevel, string> log = null)
        {
            IBlobHelper storage;
            if (string.IsNullOrEmpty(StorageAccountConnectionString))
            {
                storage = new ApiBlobHelper(HelixApi.Storage);
            }
            else
            {
                storage = new ConnectionStringBlobHelper(StorageAccountConnectionString);
            }

            IBlobContainer storageContainer = await storage.GetContainerAsync(TargetContainerName);
            var jobList = new List<JobListEntry>();

            List<string> correlationPayloadUris =
                (await Task.WhenAll(CorrelationPayloads.Select(p => p.UploadAsync(storageContainer)))).ToList();

            foreach (WorkItemDefinition workItem in _workItems)
            {
                JobListEntry entry = await workItem.SendAsync(storageContainer, TargetContainerName);
                entry.CorrelationPayloadUris = correlationPayloadUris;
                jobList.Add(entry);
            }

            string jobListJson = JsonConvert.SerializeObject(jobList);
            Uri jobListUri = await storageContainer.UploadTextAsync(
                jobListJson,
                $"job-list-{Guid.NewGuid()}.json");


            bool keepTrying = true;
            JobCreationResult newJob = null;
            System.Threading.CancellationToken cancellationToken = new System.Threading.CancellationToken();
            for (int counter = 0; keepTrying && counter < MAX_RETRIES; counter++)
            {
                try
                {
                    newJob = await JobApi.NewOperationAsync(
                        new JobCreationRequest(
                            Source,
                            Type,
                            Build,
                            _properties,
                            jobListUri.ToString(),
                            TargetQueueId,
                            storageContainer.Uri,
                            storageContainer.ReadSas,
                            storageContainer.WriteSas,
                            Creator,
                            maxRetryCount: MaxRetryCount,
                            jobStartIdentifier: _jobStartIdentifier),
                        cancellationToken);

                    keepTrying = false;
                }
                catch (TaskCanceledException e)
                {
                    // check to see if this is really an HttpClient timeout
                    if (e.CancellationToken != cancellationToken)
                    {
                        log?.Invoke(LogLevel.Informational, $"HttpClient timeout occurred while attempting to post new job to '{TargetQueueId}', will retry. Job Start Identifier: ${_jobStartIdentifier}");
                    }
                    else
                    {
                        // Something else cancelled this task so we need to throw this up; should never occur
                        throw;
                    }
                }
                catch (HttpRequestException e)
                {
                    log?.Invoke(LogLevel.Informational, $"HttpRequestException thrown attempting to submit job to Helix. Job will retry.\nException details: {e.Message}");
                }

                await Task.Delay((counter + 1) * 1000);
            }
            // if we went through all attempts and keepTrying is still true, we failed to send the job
            if (keepTrying)
            {
                log?.Invoke(LogLevel.Error, $"Unable to publish to queue '{TargetQueueId} after {MAX_RETRIES} attempts.");
            }

            return new SentJob(JobApi, newJob);
        }

        public IJobDefinitionWithTargetQueue WithBuild(string buildNumber)
        {
            Build = buildNumber;
            return this;
        }

        public IJobDefinitionWithType WithSource(string source)
        {
            Source = source;
            return this;
        }

        public IJobDefinition WithTargetQueue(string queueId)
        {
            TargetQueueId = queueId;
            return this;
        }

        public IJobDefinitionWithBuild WithType(string type)
        {
            Type = type;
            return this;
        }

        public IJobDefinition WithMaxRetryCount(int? maxRetryCount)
        {
            MaxRetryCount = maxRetryCount;
            return this;
        }

        internal void AddWorkItem(WorkItemDefinition workItemDefinition)
        {
            _workItems.Add(workItemDefinition);
        }
    }
}
