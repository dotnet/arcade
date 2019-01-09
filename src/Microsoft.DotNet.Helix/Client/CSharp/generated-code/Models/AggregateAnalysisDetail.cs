using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class AggregateAnalysisDetail
    {
        public AggregateAnalysisDetail(AggregateAnalysisDetailAnalysis analysis, string job, string workItem, IImmutableDictionary<string, string> key)
        {
            Analysis = analysis;
            Job = job;
            WorkItem = workItem;
            Key = key;
        }

        [JsonProperty("Analysis")]
        public AggregateAnalysisDetailAnalysis Analysis { get; set; }

        [JsonProperty("Job")]
        public string Job { get; set; }

        [JsonProperty("WorkItem")]
        public string WorkItem { get; set; }

        [JsonProperty("Key")]
        public IImmutableDictionary<string, string> Key { get; set; }

        public bool IsValid
        {
            get
            {
                return
                    !(Analysis == default) &&
                    !(string.IsNullOrEmpty(Job)) &&
                    !(string.IsNullOrEmpty(WorkItem)) &&
                    !(Key == default);
            }
        }
    }
}
