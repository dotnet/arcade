using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class JobSummary
    {
        public JobSummary(string detailsUrl, string name, string waitUrl, string source, string type, string build)
        {
            DetailsUrl = detailsUrl;
            Name = name;
            WaitUrl = waitUrl;
            Source = source;
            Type = type;
            Build = build;
        }

        [JsonProperty("FailureReason")]
        public FailureReason FailureReason { get; set; }

        [JsonProperty("QueueId")]
        public string QueueId { get; set; }

        [JsonProperty("DetailsUrl")]
        public string DetailsUrl { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("Creator")]
        public string Creator { get; set; }

        [JsonProperty("Created")]
        public string Created { get; set; }

        [JsonProperty("Finished")]
        public string Finished { get; set; }

        [JsonProperty("InitialWorkItemCount")]
        public int? InitialWorkItemCount { get; set; }

        [JsonProperty("WaitUrl")]
        public string WaitUrl { get; set; }

        [JsonProperty("Source")]
        public string Source { get; set; }

        [JsonProperty("Type")]
        public string Type { get; set; }

        [JsonProperty("Build")]
        public string Build { get; set; }

        [JsonProperty("Properties")]
        public JobSummaryProperties Properties { get; set; }

        [JsonProperty("Errors")]
        public IImmutableList<WorkItemError> Errors { get; set; }

        [JsonIgnore]
        public bool IsValid
        {
            get
            {
                return
                    !(string.IsNullOrEmpty(DetailsUrl)) &&
                    !(string.IsNullOrEmpty(Name)) &&
                    !(string.IsNullOrEmpty(WaitUrl)) &&
                    !(string.IsNullOrEmpty(Source)) &&
                    !(string.IsNullOrEmpty(Type)) &&
                    !(string.IsNullOrEmpty(Build));
            }
        }
    }
}
