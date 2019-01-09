using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class BuildAggregation
    {
        public BuildAggregation(string buildNumber, IImmutableDictionary<string, Newtonsoft.Json.Linq.JToken> sources)
        {
            BuildNumber = buildNumber;
            Sources = sources;
        }

        [JsonProperty("BuildNumber")]
        public string BuildNumber { get; set; }

        [JsonProperty("Sources")]
        public IImmutableDictionary<string, Newtonsoft.Json.Linq.JToken> Sources { get; }

        public bool IsValid
        {
            get
            {
                return
                    !(string.IsNullOrEmpty(BuildNumber));
            }
        }
    }
}
