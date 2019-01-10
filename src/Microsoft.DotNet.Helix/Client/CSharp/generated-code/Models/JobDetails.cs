using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class JobDetails
    {
        public JobDetails(string jobList, JobWorkItemCounts workItems, string name, string waitUrl, string source, string type, string build)
        {
            JobList = jobList;
            WorkItems = workItems;
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

        [JsonProperty("JobList")]
        public string JobList { get; set; }

        [JsonProperty("WorkItems")]
        public JobWorkItemCounts WorkItems { get; set; }

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
        public JobDetailsProperties Properties { get; set; }

        [JsonProperty("Errors")]
        public IImmutableList<WorkItemError> Errors { get; set; }

        [JsonIgnore]
        public bool IsValid
        {
            get
            {
                return
                    !(string.IsNullOrEmpty(JobList)) &&
                    !(WorkItems == default) &&
                    !(string.IsNullOrEmpty(Name)) &&
                    !(string.IsNullOrEmpty(WaitUrl)) &&
                    !(string.IsNullOrEmpty(Source)) &&
                    !(string.IsNullOrEmpty(Type)) &&
                    !(string.IsNullOrEmpty(Build));
            }
        }
    }
}
