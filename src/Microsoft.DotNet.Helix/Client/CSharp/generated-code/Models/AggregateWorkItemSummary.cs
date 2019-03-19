using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class AggregateWorkItemSummary
    {
        public AggregateWorkItemSummary(IImmutableList<WorkItemAggregateSummary> workItems, IImmutableList<KeyedData> analyses)
        {
            WorkItems = workItems;
            Analyses = analyses;
        }

        [JsonProperty("WorkItems")]
        public IImmutableList<WorkItemAggregateSummary> WorkItems { get; set; }

        [JsonProperty("Analyses")]
        public IImmutableList<KeyedData> Analyses { get; set; }

        [JsonIgnore]
        public bool IsValid
        {
            get
            {
                if (WorkItems == default)
                {
                    return false;
                }
                if (Analyses == default)
                {
                    return false;
                }
                return true;
            }
        }
    }
}
