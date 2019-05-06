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

        [JsonIgnore]
        public bool IsValid
        {
            get
            {
                if (Key == default)
                {
                    return false;
                }
                if (Data == default)
                {
                    return false;
                }
                return true;
            }
        }
    }
}
