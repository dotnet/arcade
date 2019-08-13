using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.Rest;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client
{
    internal class JobDefinition : IJobDefinitionWithType,
        IJobDefinitionWithTargetQueue,
        IJobDefinition
    {
        private readonly Dictionary<string, string> _properties;
        private readonly List<WorkItemDefinition> _workItems;

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

            var (queueId, dockerTag, queueAlias) = ParseQueueId(TargetQueueId);

            IBlobContainer storageContainer = await storage.GetContainerAsync(TargetContainerName, queueId);
            var jobList = new List<JobListEntry>();

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
                // Container names can only be alphanumeric (plus dashes) lowercase names, with no consecutive dashes.
                // Replace / with -, make all branch and repository names lowercase, remove any characters not
                // allowed in container names, and replace any string of dashes with a single dash.
                Regex illegalCharacters = new Regex("[^a-z0-9-]");
                Regex multipleDashes = new Regex("-{2,}");

                string repoName = Environment.GetEnvironmentVariable("BUILD_REPOSITORY_NAME");
                string branchName = Environment.GetEnvironmentVariable("BUILD_SOURCEBRANCH");

                // ResultContainerPrefix will be <Repository Name>-<BranchName>
                ResultContainerPrefix = $"{repoName}-{branchName}-".Replace("/", "-").ToLower();
                ResultContainerPrefix  = multipleDashes.Replace(illegalCharacters.Replace(ResultContainerPrefix, ""), "-");
            }

            var creationRequest = new JobCreationRequest(Type, _properties.ToImmutableDictionary(), jobListUri.ToString(), queueId)
            {
                Creator = Creator,
                ResultContainerPrefix = ResultContainerPrefix,
                DockerTag = dockerTag,
                QueueAlias = queueAlias,
            };

            if (string.IsNullOrEmpty(Source))
            {
                // We only want to specify a branch if Source wasn't already provided.
                // Latest Helix Job API will 400 if both Source and any of SourcePrefix, TeamProject, Repository, or Branch are set.
                InitializeSourceParameters(creationRequest);
            }
            else
            {
                creationRequest.Source = Source;
            }

            string jobStartIdentifier = Guid.NewGuid().ToString("N");
            JobCreationResult newJob = await HelixApi.RetryAsync(
                () => JobApi.NewAsync(creationRequest, jobStartIdentifier),
                ex => log?.Invoke($"Starting job failed with {ex}\nRetrying..."),
                IsRetryableJobListUriHttpError,
                CancellationToken.None);

            return new SentJob(JobApi, newJob, newJob.ResultsUri, newJob.ResultsUriRSAS);
        }

        private (string queueId, string dockerTag, string queueAlias) ParseQueueId(string value)
        {
            var @index = value.IndexOf('@');
            if (@index < 0)
            {
                return (value, string.Empty, value);
            }

            string queueInfo = value.Substring(0, @index);
            string dockerTag = value.Substring(@index + 1);

            string queueAlias;
            string queueId;

            Match queueInfoSplit = new Regex(@"\((.+?)\)(.*)").Match(queueInfo);
            if (queueInfoSplit.Success && queueInfoSplit.Groups.Count == 3)
            {
                queueAlias = queueInfoSplit.Groups[1].Value;
                queueId = queueInfoSplit.Groups[2].Value;
            }
            else
            {
                queueId = queueAlias = queueInfo;
            }

            return (queueId, dockerTag, queueAlias);
        }

        private string GetRequiredEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name) ?? throw new ArgumentException("Missing required environment variable", name);
        }

        private void InitializeSourceParameters(JobCreationRequest creationRequest)
        {
            creationRequest.Branch = GetRequiredEnvironmentVariable("BUILD_SOURCEBRANCH");
            creationRequest.Repository = GetRequiredEnvironmentVariable("BUILD_REPOSITORY_NAME");
            creationRequest.TeamProject = GetRequiredEnvironmentVariable("SYSTEM_TEAMPROJECT");
            creationRequest.SourcePrefix = GetSourcePrefix();
        }

        private string GetSourcePrefix()
        {
            var reason = GetRequiredEnvironmentVariable("BUILD_REASON");
            if (string.Equals(reason, "PullRequest", StringComparison.OrdinalIgnoreCase))
            {
                return "pr";
            }

            var teamProject = GetRequiredEnvironmentVariable("SYSTEM_TEAMPROJECT");
            if (string.Equals(teamProject, "internal", StringComparison.OrdinalIgnoreCase))
            {
                return "official";
            }

            return "ci";
        }

        public bool IsRetryableJobListUriHttpError(Exception ex)
        {
            if (ex.Message.Contains("Provided Job List Uri is not accessible") && ex.InnerException is RestApiException raex && (int)raex.Response.StatusCode == 400)
            {
                return true;
            }

            return Client.HelixApi.IsRetryableHttpException(ex);
        }

        public IJobDefinitionWithTargetQueue WithBuild(string buildNumber)
        {
            Build = buildNumber;
            return this;
        }

        public IJobDefinition WithSource(string source)
        {
            Source = source;
            return this;
        }

        public IJobDefinition WithTargetQueue(string queueId)
        {
            TargetQueueId = queueId;
            return this;
        }

        public IJobDefinitionWithTargetQueue WithType(string type)
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
