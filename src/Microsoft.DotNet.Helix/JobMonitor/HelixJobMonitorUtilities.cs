// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    public sealed class AzureDevOpsTimelineRecord
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("parentId")]
        public string ParentId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("refName")]
        public string ReferenceName { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("attempt")]
        public int Attempt { get; set; } = 1;

        [JsonProperty("previousAttempts")]
        public PreviousAttemptReference[] PreviousAttempts { get; set; }
    }

    public sealed class PreviousAttemptReference
    {
        [JsonProperty("attempt")]
        public int Attempt { get; set; }

        [JsonProperty("timelineId")]
        public string TimelineId { get; set; }

        [JsonProperty("recordId")]
        public string RecordId { get; set; }
    }

    public static class HelixJobMonitorUtilities
    {
        public static bool AreNonMonitorJobsComplete(IEnumerable<AzureDevOpsTimelineRecord> records, string jobMonitorName)
            => GetRelevantJobRecords(records, jobMonitorName).All(IsTerminal);

        public static bool HasFailedNonMonitorJobs(IEnumerable<AzureDevOpsTimelineRecord> records, string jobMonitorName)
            => HasFailedNonMonitorJobs(records, jobMonitorName, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        public static bool HasFailedNonMonitorJobs(
            IEnumerable<AzureDevOpsTimelineRecord> records,
            string jobMonitorName,
            IReadOnlySet<string> ignoredJobNames)
            => GetRelevantJobRecords(records, jobMonitorName)
                .Where(r => ignoredJobNames == null || !ignoredJobNames.Contains(r.ReferenceName))
                .Any(r =>
                    string.Equals(r.Result, "failed", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(r.Result, "canceled", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Returns the subset of <paramref name="records"/> that belongs to the pipeline stage
        /// named <paramref name="stageName"/>, including the Stage record itself and any
        /// descendant records (Phases, Jobs, Tasks). When the named Stage is not present in the
        /// timeline an empty list is returned.
        /// </summary>
        public static IReadOnlyList<AzureDevOpsTimelineRecord> FilterRecordsToStage(
            IEnumerable<AzureDevOpsTimelineRecord> records,
            string stageName)
        {
            if (string.IsNullOrEmpty(stageName))
            {
                return [.. records];
            }

            List<AzureDevOpsTimelineRecord> all = records.ToList();
            AzureDevOpsTimelineRecord stageRoot = all.FirstOrDefault(r =>
                string.Equals(r.Type, "Stage", StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.ReferenceName, stageName, StringComparison.OrdinalIgnoreCase));

            if (stageRoot == null)
            {
                return [];
            }

            // Iteratively collect all descendants of the stage record by following ParentId.
            var byParent = all
                .Where(r => !string.IsNullOrEmpty(r.ParentId))
                .ToLookup(r => r.ParentId, StringComparer.OrdinalIgnoreCase);

            var result = new List<AzureDevOpsTimelineRecord> { stageRoot };
            var queue = new Queue<string>();
            queue.Enqueue(stageRoot.Id);
            while (queue.Count > 0)
            {
                string parentId = queue.Dequeue();
                foreach (AzureDevOpsTimelineRecord child in byParent[parentId])
                {
                    result.Add(child);
                    if (!string.IsNullOrEmpty(child.Id))
                    {
                        queue.Enqueue(child.Id);
                    }
                }
            }

            return result;
        }

        private static IEnumerable<AzureDevOpsTimelineRecord> GetRelevantJobRecords(IEnumerable<AzureDevOpsTimelineRecord> records, string jobMonitorName)
        {
            return (records ?? [])
                .Where(r => string.Equals(r.Type, "Job", StringComparison.OrdinalIgnoreCase))
                .Where(r => !string.Equals(r.ReferenceName, jobMonitorName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsTerminal(AzureDevOpsTimelineRecord record)
            => string.Equals(record?.State, "completed", StringComparison.OrdinalIgnoreCase);
    }
}
