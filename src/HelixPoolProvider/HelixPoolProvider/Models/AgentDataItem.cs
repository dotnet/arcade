using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentDataItem
    {
        public string correlationId { get; set; }
        public string queueId { get; set; }
        public string workItemId { get; set; }
    }
}
