using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class ExternalLinkData
    {
        public ExternalLinkData()
        {
        }

        [JsonProperty("WorkItemStatus")]
        public string WorkItemStatus { get; set; }

        [JsonProperty("WorkItemStatusMessage")]
        public string WorkItemStatusMessage { get; set; }

        [JsonProperty("Description")]
        public string Description { get; set; }

        [JsonProperty("Uri")]
        public string Uri { get; set; }

        [JsonProperty("WarningCount")]
        public string WarningCount { get; set; }

        [JsonProperty("Categories")]
        public IImmutableList<string> Categories { get; set; }

        [JsonProperty("FailureReason")]
        public FailureReason FailureReason { get; set; }
    }
}
