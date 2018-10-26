using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentInfoItem
    {
        public AgentDataItem agentData { get; set; }
        public bool accepted { get; set; }
    }
}
