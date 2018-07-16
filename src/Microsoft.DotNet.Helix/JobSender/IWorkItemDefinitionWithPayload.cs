using System;
using System.Text;

namespace Microsoft.DotNet.Helix.Client
{
    public interface IWorkItemDefinitionWithPayload
    {
        IWorkItemDefinition WithPayloadUri(Uri payloadUri);
        IWorkItemDefinition WithFiles(params string[] files);
        IWorkItemDefinition WithDirectoryPayload(string directory);
        IWorkItemDefinition WithSingleFilePayload(string name, string content);
        IWorkItemDefinition WithSingleFilePayload(string name, string content, Encoding encoding);
        IWorkItemDefinition WithSingleFilePayload(string name, byte[] content);
        IWorkItemDefinition WithEmptyPayload();
    }
}
