using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class WorkItemAggregateSummary
    {
        public WorkItemAggregateSummary()
        {
        }

        [JsonProperty("JobId")]
        public int? JobId { get; set; }

        [JsonProperty("WorkItemId")]
        public int? WorkItemId { get; set; }

        [JsonProperty("MachineName")]
        public string MachineName { get; set; }

        [JsonProperty("Job")]
        public string Job { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("Guid")]
        public string Guid { get; set; }

        [JsonProperty("QueueTime")]
        public DateTimeOffset? QueueTime { get; set; }

        [JsonProperty("StartTime")]
        public DateTimeOffset? StartTime { get; set; }

        [JsonProperty("FinishedTime")]
        public DateTimeOffset? FinishedTime { get; set; }

        [JsonProperty("ExitCode")]
        public int? ExitCode { get; set; }

        [JsonProperty("ConsoleOutputUri")]
        public string ConsoleOutputUri { get; set; }

        [JsonProperty("Logs")]
        public IImmutableList<WorkItemLog> Logs { get; set; }

        [JsonProperty("Errors")]
        public IImmutableList<WorkItemError> Errors { get; set; }

        [JsonProperty("Warnings")]
        public IImmutableList<WorkItemError> Warnings { get; set; }

        [JsonProperty("OtherEvents")]
        public IImmutableList<UnknownWorkItemEvent> OtherEvents { get; set; }

        [JsonProperty("Passed")]
        public bool? Passed { get; set; }

        [JsonProperty("Attempt")]
        public int? Attempt { get; set; }

        [JsonProperty("State")]
        public string State { get; set; }

        [JsonProperty("Key")]
        public IImmutableDictionary<string, string> Key { get; set; }
    }
}
