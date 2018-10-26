using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentReleaseItem
    {
        public string agentId { get; set; }
        public string accountId { get; set; }
        public string agentCloudId { get; set; }
        public string agentPool { get; set; }
        public AgentDataItem agentData { get; set; }
    }
}
