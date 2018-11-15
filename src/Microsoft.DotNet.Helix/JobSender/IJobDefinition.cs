using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    public interface IJobDefinition
    {
        IWorkItemDefinitionWithCommand DefineWorkItem(string workItemName);
        IJobDefinition WithCorrelationPayloadUris(params Uri[] payloadUris);
        IJobDefinition WithCorrelationPayloadDirectory(string directory);
        IJobDefinition WithCorrelationPayloadDirectory(string directory, bool includeDirectoryName);
        IJobDefinition WithCorrelationPayloadArchive(string archive);
        IJobDefinition WithCorrelationPayloadFiles(params string[] files);
        IJobDefinition WithProperty(string key, string value);
        IJobDefinition WithCreator(string creator);
        IJobDefinition WithContainerName(string targetContainerName);
        IJobDefinition WithStorageAccountConnectionString(string accountConnectionString);
        Task<ISentJob> SendAsync();
    }
}
