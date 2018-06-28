using System;

namespace Microsoft.DotNet.Helix.Client
{
    public interface IWorkItemDefinitionWithPayload
    {
        IWorkItemDefinitionWithCorrelationPayload WithPayloadUri(Uri payloadUri);
        IWorkItemDefinitionWithCorrelationPayload WithFiles(params string[] files);
        IWorkItemDefinitionWithCorrelationPayload WithEmptyPayload();
    }
}
