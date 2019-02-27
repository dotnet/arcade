using Microsoft.WindowsAzure.Storage;
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
        IJobDefinition WithCorrelationPayloadDirectory(string directory, string archiveEntryPrefix);
        IJobDefinition WithCorrelationPayloadArchive(string archive);
        IJobDefinition WithCorrelationPayloadFiles(params string[] files);
        IJobDefinition WithProperty(string key, string value);
        IJobDefinition WithCreator(string creator);
        IJobDefinition WithContainerName(string targetContainerName);
        IJobDefinition WithStorageAccountConnectionString(string accountConnectionString);
        IJobDefinition WithResultsContainerName(string resultsContainerName);
        IJobDefinition WithResultsStorageAccountConnectionString(string resultsAccountConnectionString);
        IJobDefinition WithDefaultResultsContainer();
        IJobDefinition WithMaxRetryCount(int? maxRetryCount);
        Task<ISentJob> SendAsync(Action<string> log = null);
    }
}
