using Microsoft.DotNet.Helix.Client.Models;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentDefinitionItem
    {
        public AgentDefinitionItem(QueueInfo info, string agentDefinitionUrl)
        {
            identifier = info.QueueId;
            url = agentDefinitionUrl;
            metadataDocumentUrl = null;
            workspacePath = info.WorkspacePath;
            isInternal = info.IsInternalOnly.Value;
            available = info.IsAvailable.Value;
        }

        // Required data
        public string identifier { get; set; }
        public string url { get; set; }
        public string metadataDocumentUrl { get; set; }

        // Optional data
        public string workspacePath { get; set; }
        public bool isInternal { get; set; }
        public bool available { get; set; }
    }
}
