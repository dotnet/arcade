using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class BuildAggregationSource
    {
        public BuildAggregationSource(IImmutableDictionary<string, AggregatedWorkItemCounts> types)
        {
            Types = types;
        }

        [JsonProperty("Types")]
        public IImmutableDictionary<string, AggregatedWorkItemCounts> Types { get; }
    }
}
