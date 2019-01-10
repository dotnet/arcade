using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class QueueInfo
    {
        public QueueInfo()
        {
        }

        [JsonProperty("Description")]
        public string Description { get; set; }

        [JsonProperty("IsAvailable")]
        public bool? IsAvailable { get; set; }

        [JsonProperty("IsInternalOnly")]
        public bool? IsInternalOnly { get; set; }

        [JsonProperty("IsOnPremises")]
        public bool? IsOnPremises { get; set; }

        [JsonProperty("OperatingSystemGroup")]
        public string OperatingSystemGroup { get; set; }

        [JsonProperty("QueueId")]
        public string QueueId { get; set; }

        [JsonProperty("QueueDepth")]
        public long? QueueDepth { get; set; }

        [JsonProperty("UserList")]
        public string UserList { get; set; }

        [JsonProperty("WorkspacePath")]
        public string WorkspacePath { get; set; }
    }
}
