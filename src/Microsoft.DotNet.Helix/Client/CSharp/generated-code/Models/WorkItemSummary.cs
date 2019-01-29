using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class WorkItemSummary
    {
        public WorkItemSummary(string detailsUrl, string job, string name, string state)
        {
            DetailsUrl = detailsUrl;
            Job = job;
            Name = name;
            State = state;
        }

        [JsonProperty("DetailsUrl")]
        public string DetailsUrl { get; set; }

        [JsonProperty("Job")]
        public string Job { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("State")]
        public string State { get; set; }

        [JsonIgnore]
        public bool IsValid
        {
            get
            {
                return
                    !(string.IsNullOrEmpty(DetailsUrl)) &&
                    !(string.IsNullOrEmpty(Job)) &&
                    !(string.IsNullOrEmpty(Name)) &&
                    !(string.IsNullOrEmpty(State));
            }
        }
    }
}
