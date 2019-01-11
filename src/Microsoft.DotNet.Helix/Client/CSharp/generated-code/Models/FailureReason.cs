using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class FailureReason
    {
        public FailureReason()
        {
        }

        [JsonProperty("Issue")]
        public FailureReasonPart Issue { get; set; }

        [JsonProperty("Owner")]
        public FailureReasonPart Owner { get; set; }
    }
}
