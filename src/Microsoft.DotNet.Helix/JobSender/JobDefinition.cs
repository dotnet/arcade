using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        private bool _withDefaultResultsContainer;

        public JobDefinition(IJob jobApi)
        {
            _workItems = new List<WorkItemDefinition>();
            WorkItems = _workItems.AsReadOnly();
            _properties = new Dictionary<string, string>();
            Properties = new ReadOnlyDictionary<string, string>(_properties);
            JobApi = jobApi;
            HelixApi = ((IServiceOperations<HelixApi>) JobApi).Client;
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
        public string ResultsStorageAccountConnectionString { get; private set; }
        public string TargetResultsContainerName { get; set; } = DefaultContainerName;
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

        public IJobDefinition WithResultsContainerName(string resultsContainerName)
        {
            TargetResultsContainerName = resultsContainerName;
            return this;
        }

        public IJobDefinition WithResultsStorageAccountConnectionString(string resultsAccountConnectionString)
        {
            ResultsStorageAccountConnectionString = resultsAccountConnectionString;
            return this;
        }

        public IJobDefinition WithDefaultResultsContainer()
        {
            _withDefaultResultsContainer = true;
            return this;
        }

        public async Task<ISentJob> SendAsync(Action<string> log = null)
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

            IBlobContainer resultsStorageContainer = null;
            if (!string.IsNullOrEmpty(ResultsStorageAccountConnectionString))
            {

                IBlobHelper resultsStorage = new ConnectionStringBlobHelper(ResultsStorageAccountConnectionString);
                resultsStorageContainer = await resultsStorage.GetContainerAsync(TargetResultsContainerName);
            }
            else if (_withDefaultResultsContainer)
            {
                resultsStorageContainer = await storage.GetContainerAsync(TargetResultsContainerName);
            }

            List<string> correlationPayloadUris =
                (await Task.WhenAll(CorrelationPayloads.Select(p => p.UploadAsync(storageContainer, log)))).ToList();

            jobList = (await Task.WhenAll(
                _workItems.Select(async w =>
                {
                    var entry = await w.SendAsync(storageContainer, TargetContainerName, log);
                    entry.CorrelationPayloadUris = correlationPayloadUris;
                    return entry;
                }
                ))).ToList();

            string jobListJson = JsonConvert.SerializeObject(jobList);
            Uri jobListUri = await storageContainer.UploadTextAsync(
                jobListJson,
                $"job-list-{Guid.NewGuid()}.json");


            string jobStartIdentifier = Guid.NewGuid().ToString("N");
            JobCreationResult newJob = await HelixApi.RetryAsync(
                () => JobApi.NewAsync(
                    new JobCreationRequest(
                        Source,
                        Type,
                        Build,
                        _properties.ToImmutableDictionary(),
                        jobListUri.ToString(),
                        TargetQueueId)
                    {
                        Creator = Creator,
                        MaxRetryCount = MaxRetryCount ?? 0,
                        JobStartIdentifier = jobStartIdentifier,
                        ResultsUri = resultsStorageContainer?.Uri,
                        ResultsUriRSAS = resultsStorageContainer?.ReadSas,
                        ResultsUriWSAS = resultsStorageContainer?.WriteSas,
                    }),
                ex => log?.Invoke($"Starting job failed with {ex}\nRetrying..."));


            return new SentJob(JobApi, newJob, resultsStorageContainer?.Uri, string.IsNullOrEmpty(Creator) ? resultsStorageContainer?.ReadSas : string.Empty);
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
