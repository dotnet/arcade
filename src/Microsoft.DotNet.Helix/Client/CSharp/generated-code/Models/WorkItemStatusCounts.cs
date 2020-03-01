using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class WorkItemStatusCounts
    {
        public WorkItemStatusCounts(IImmutableList<AnalysisCount> analysis, IImmutableDictionary<string, int> workItemStatus)
        {
            Analysis = analysis;
            WorkItemStatus = workItemStatus;
        }

        [JsonProperty("Analysis")]
        public IImmutableList<AnalysisCount> Analysis { get; set; }

        [JsonProperty("WorkItemStatus")]
        public IImmutableDictionary<string, int> WorkItemStatus { get; set; }

        [JsonIgnore]
        public bool IsValid
        {
            get
            {
                if (Analysis == default(IImmutableList<AnalysisCount>))
                {
                    return false;
                }
                if (WorkItemStatus == default(IImmutableDictionary<string, int>))
                {
                    return false;
                }
                return true;
            }
        }
    }
}
