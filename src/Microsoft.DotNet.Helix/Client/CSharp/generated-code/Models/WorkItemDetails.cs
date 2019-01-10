using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class WorkItemDetails
    {
        public WorkItemDetails(string id, string machineName, string job, string name, string state)
        {
            Id = id;
            MachineName = machineName;
            Job = job;
            Name = name;
            State = state;
        }

        [JsonProperty("FailureReason")]
        public FailureReason FailureReason { get; set; }

        [JsonProperty("Id")]
        public string Id { get; set; }

        [JsonProperty("MachineName")]
        public string MachineName { get; set; }

        [JsonProperty("ExitCode")]
        public int ExitCode { get; set; }

        [JsonProperty("ConsoleOutputUri")]
        public string ConsoleOutputUri { get; set; }

        [JsonProperty("Errors")]
        public IImmutableList<WorkItemError> Errors { get; set; }

        [JsonProperty("Warnings")]
        public IImmutableList<WorkItemError> Warnings { get; set; }

        [JsonProperty("Logs")]
        public IImmutableList<WorkItemLog> Logs { get; set; }

        [JsonProperty("Analysis")]
        public AnalysisReport Analysis { get; set; }

        [JsonProperty("OtherEvents")]
        public IImmutableList<WorkItemDetailsOtherEvents> OtherEvents { get; set; }

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
                    !(string.IsNullOrEmpty(Id)) &&
                    !(string.IsNullOrEmpty(MachineName)) &&
                    !(string.IsNullOrEmpty(Job)) &&
                    !(string.IsNullOrEmpty(Name)) &&
                    !(string.IsNullOrEmpty(State));
            }
        }
    }
}
