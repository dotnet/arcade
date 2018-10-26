using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentAcquireItem
    {
        public string agentId { get; set; }
        public string agentPool { get; set; }
        public string accountId { get; set; }
        public string failRequestUrl { get; set; }
        public string appendRequestMessageUrl { get; set; }
        public AgentConfigurationItem agentConfiguration { get; set; } 
        public object agentSpecification { get; set; }
    }
}
