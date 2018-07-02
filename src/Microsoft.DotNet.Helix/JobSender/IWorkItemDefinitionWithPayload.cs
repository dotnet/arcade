using System;

namespace Microsoft.DotNet.Helix.Client
{
    public interface IWorkItemDefinitionWithPayload
    {
        IWorkItemDefinition WithPayloadUri(Uri payloadUri);
        IWorkItemDefinition WithFiles(params string[] files);
        IWorkItemDefinition WithEmptyPayload();
    }
}
