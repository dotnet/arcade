using Microsoft.WindowsAzure.Storage;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    public interface IJobDefinition
    {
        IWorkItemDefinitionWithCommand DefineWorkItem(string workItemName);
        IJobDefinition WithCorrelationPayloadUris(params Uri[] payloadUris);
        IJobDefinition WithCorrelationPayloadUris(IDictionary<Uri, string> payloadUrisWithDestinations);
        IJobDefinition WithCorrelationPayloadDirectory(string directory, string destination = "");
        IJobDefinition WithCorrelationPayloadDirectory(string directory, bool includeDirectoryName, string destination = "");
        IJobDefinition WithCorrelationPayloadDirectory(string directory, string archiveEntryPrefix, string destination);
        IJobDefinition WithCorrelationPayloadArchive(string archive, string destination = "");
        IJobDefinition WithCorrelationPayloadFiles(params string[] files);
        IJobDefinition WithCorrelationPayloadFiles(IList<string> files, string destination);
        IJobDefinition WithSource(string source);
        IJobDefinition WithProperty(string key, string value);
        IJobDefinition WithCreator(string creator);
        IJobDefinition WithContainerName(string targetContainerName);
        IJobDefinition WithStorageAccountConnectionString(string accountConnectionString);
        IJobDefinition WithResultsContainerName(string resultsContainerName);
        IJobDefinition WithMaxRetryCount(int? maxRetryCount);
        Task<ISentJob> SendAsync(Action<string> log = null);
    }
}
