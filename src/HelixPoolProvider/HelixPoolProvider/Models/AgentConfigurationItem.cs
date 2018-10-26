using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentConfigurationItem
    {
        public AgentSettingsItem agentSettings { get; set; }
        public string agentVersion { get; set; }
        public AgentCredentialsItem agentCredentials { get; set; }
        public object agentDownloadUrls { get; set; }
    }
}
