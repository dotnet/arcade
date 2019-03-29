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
        public string ResultContainerPrefix { get; private set; }
        public IDictionary<IPayload, string> CorrelationPayloads { get; } = new Dictionary<IPayload, string>();
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
                CorrelationPayloads.Add(new UriPayload(uri), "");
            }
            return this;
        }

        public IJobDefinition WithCorrelationPayloadUris(IDictionary<Uri, string> payloadUrisWithDestinations)
        {
            foreach (var (uri, destination) in payloadUrisWithDestinations)
            {
                CorrelationPayloads.Add(new UriPayload(uri), destination);
            }
            return this;
        }

        public IJobDefinition WithCorrelationPayloadDirectory(string directory, string destination = "")
        {
            return WithCorrelationPayloadDirectory(directory, false, destination);
        }

        public IJobDefinition WithCorrelationPayloadDirectory(string directory, bool includeDirectoryName, string destination = "")
        {
            string archiveEntryPrefix = null;
            if (includeDirectoryName)
            {
                archiveEntryPrefix = new DirectoryInfo(directory).Name;
            }
            return WithCorrelationPayloadDirectory(directory, archiveEntryPrefix, destination);
        }

        public IJobDefinition WithCorrelationPayloadDirectory(string directory, string archiveEntryPrefix, string destination)
        {
            CorrelationPayloads.Add(new DirectoryPayload(directory, archiveEntryPrefix), destination);
            return this;
        }

        public IJobDefinition WithCorrelationPayloadFiles(params string[] files)
        {
            CorrelationPayloads.Add(new AdhocPayload(files), "");
            return this;
        }

        public IJobDefinition WithCorrelationPayloadFiles(IList<string> files, string destination)
        {
            CorrelationPayloads.Add(new AdhocPayload(files.ToArray()), destination);
            return this;
        }

        public IJobDefinition WithCorrelationPayloadArchive(string archive, string destination = "")
        {
            CorrelationPayloads.Add(new ArchivePayload(archive), destination);
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

            Dictionary<string, string> correlationPayloadUris =
                (await Task.WhenAll(CorrelationPayloads.Select(async p => (uri: await p.Key.UploadAsync(storageContainer, log), destination: p.Value)))).ToDictionary(x => x.uri, x => x.destination);

            jobList = (await Task.WhenAll(
                _workItems.Select(async w =>
                {
                    var entry = await w.SendAsync(storageContainer, TargetContainerName, log);
                    entry.CorrelationPayloadUrisWithDestinations = correlationPayloadUris;
                    return entry;
                }
                ))).ToList();

            string jobListJson = JsonConvert.SerializeObject(jobList);
            Uri jobListUri = await storageContainer.UploadTextAsync(
                jobListJson,
                $"job-list-{Guid.NewGuid()}.json");
            // Don't log the sas, remove the query string.
            string jobListUriForLogging = jobListUri.ToString().Replace(jobListUri.Query, "");
            log?.Invoke($"Created job list at {jobListUriForLogging}");

            // Only specify the ResultContainerPrefix if both repository name and source branch are available.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUILD_REPOSITORY_NAME")) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCH")))
            {
                // Remove all slashes from repository name and branch name. Also remove refs/, heads/, and pulls/ from branch name.
                // ResultContainerPrefix will be <Repository Name>-<BranchName>
                string repoName = Environment.GetEnvironmentVariable("BUILD_REPOSITORY_NAME").Replace("/", "-");
                string branchName = Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCH")
                    .Replace("/","-");
                ResultContainerPrefix = $"{repoName}.{branchName}-";
            }

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
                        ResultContainerPrefix = ResultContainerPrefix,
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
