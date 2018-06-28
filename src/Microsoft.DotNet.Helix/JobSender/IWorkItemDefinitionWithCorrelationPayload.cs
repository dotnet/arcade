using System;

namespace Microsoft.DotNet.Helix.Client
{
    public interface IWorkItemDefinitionWithCorrelationPayload
    {
        IWorkItemDefinitionWithCorrelationPayload WithCorrelationPayloadUris(params Uri[] payloadUris);
        IWorkItemDefinitionWithCorrelationPayload WithCorrelationPayloadFiles(params string[] files);
        IWorkItemDefinitionWithCorrelationPayload WithTimeout(TimeSpan timeout);
        IJobDefinition AttachToJob();
    }
}
