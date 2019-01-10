using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class ExternalLinkResult
    {
        public ExternalLinkResult()
        {
        }

        [JsonProperty("Links")]
        public IImmutableList<ExternalLinkData> Links { get; set; }
    }
}
