using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class RegisterConnectionItem
    {
        public string accountId { get; set; }
        public string agentCloudId { get; set; }
        // TODO: More strongly typed
        public object agentCloudConfiguration { get; set; }
    }
}
