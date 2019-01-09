using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class KeyedData
    {
        public KeyedData(IImmutableDictionary<string, string> key, AggregateAnalysisSummary data)
        {
            Key = key;
            Data = data;
        }

        [JsonProperty("Key")]
        public IImmutableDictionary<string, string> Key { get; set; }

        [JsonProperty("Data")]
        public AggregateAnalysisSummary Data { get; set; }

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
