using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    internal class WorkItemDefinition : IWorkItemDefinitionWithCommand,
        IWorkItemDefinitionWithPayload,
        IWorkItemDefinitionWithCorrelationPayload,
        IWorkItemDefinition
    {
        public WorkItemDefinition(JobDefinition jobDefinition, string workItemName)
        {
            JobDefinition = jobDefinition;
            WorkItemName = workItemName;
            Timeout = DefaultTimeout;
        }

        public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(300);
        public JobDefinition JobDefinition { get; }
        public string Command { get; private set; }
        public string WorkItemName { get; private set; }
        public TimeSpan Timeout { get; private set; }
        public IPayload Payload { get; private set; }
        public IList<IPayload> CorrelationPayloads { get; } = new List<IPayload>();

        public IWorkItemDefinitionWithPayload WithCommand(string command)
        {
            Command = command;
            return this;
        }

        public IWorkItemDefinitionWithCorrelationPayload WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public IWorkItemDefinitionWithCorrelationPayload WithCorrelationPayloadUris(params Uri[] payloadUris)
        {
            foreach (Uri uri in payloadUris)
            {
                CorrelationPayloads.Add(new UriPayload(uri));
            }
            return this;
        }

        public IWorkItemDefinitionWithCorrelationPayload WithCorrelationPayloadFiles(params string[] files)
        {
            CorrelationPayloads.Add(new AdhocPayload(files));
            return this;
        }

        public IJobDefinition AttachToJob()
        {
            JobDefinition.AddWorkItem(this);
            return JobDefinition;
        }

        public IWorkItemDefinitionWithCorrelationPayload WithPayloadUri(Uri payloadUri)
        {
            Payload = new UriPayload(payloadUri);
            return this;
        }

        public IWorkItemDefinitionWithCorrelationPayload WithFiles(params string[] files)
        {
            Payload = new AdhocPayload(files);
            return this;
        }

        public IWorkItemDefinitionWithCorrelationPayload WithEmptyPayload()
        {
            Payload = EmptyPayload.Instance;
            return this;
        }

        public async Task<JobListEntry> SendAsync(IBlobContainer payloadStorage, string containerName)
        {
            return new JobListEntry
            {
                WorkItemId = WorkItemName,
                Command = Command,
                TimeoutInSeconds = (int) Timeout.TotalSeconds,
                PayloadUri = await Payload.UploadAsync(payloadStorage),
                CorrelationPayloadUris =
                    (await Task.WhenAll(CorrelationPayloads.Select(p => p.UploadAsync(payloadStorage))))
                    .ToList()
            };
        }
    }
}
