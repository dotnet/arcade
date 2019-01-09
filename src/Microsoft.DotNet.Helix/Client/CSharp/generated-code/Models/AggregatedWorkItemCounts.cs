using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class AggregatedWorkItemCounts
    {
        public AggregatedWorkItemCounts(IImmutableDictionary<string, string> key, WorkItemStatusCounts data)
        {
            Key = key;
            Data = data;
        }

        [JsonProperty("Key")]
        public IImmutableDictionary<string, string> Key { get; set; }

        [JsonProperty("Data")]
        public WorkItemStatusCounts Data { get; set; }

        public bool IsValid
        {
            get
            {
                return
                    !(Key == default) &&
                    !(Data == default);
            }
        }
    }
}
