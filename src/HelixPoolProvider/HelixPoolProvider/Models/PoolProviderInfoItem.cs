using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class PoolProviderInfoItem
    {
        public string poolProviderProtocolVersion { get; set; }
        public string poolProviderVersion { get; set; }
        public string acquireAgentUrl { get; set; }
        public string releaseAgentUrl { get; set; }
        public string getAgentDefinitionsUrl { get; set; }
        public string getAgentRequestStatusUrl { get; set; }
        public string getAccountParallelismUrl { get; set; }
    }
}
