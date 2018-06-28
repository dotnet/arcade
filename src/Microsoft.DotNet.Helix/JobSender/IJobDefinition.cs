using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    public interface IJobDefinition
    {
        IWorkItemDefinitionWithCommand DefineWorkItem(string workItemName);
        IJobDefinition WithProperty(string key, string value);
        IJobDefinition WithCreator(string creator);
        IJobDefinition WithContainerName(string targetContainerName);
        IJobDefinition WithStorageAccountConnectionString(string accountConnectionString);
        Task<ISentJob> SendAsync();
    }
}
