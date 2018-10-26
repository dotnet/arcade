using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentSettingsItem
    {
        public string AgentId { get; set; }
        public string AgentName { get; set; }
        public string AutoUpdate { get; set; }
        public string PoolId { get; set; }
        public string ServerUrl { get; set; }
        public string SkipCapabilitiesScan { get; set; }
        public string SkipSessionRecover { get; set; }
        public string workFolder { get; set; }
    }
}
