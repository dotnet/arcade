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

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }
    }

    public static class HelixJobMonitorUtilities
    {
        public static bool AreNonMonitorJobsComplete(IEnumerable<AzureDevOpsTimelineRecord> records, string jobMonitorName)
            => GetRelevantJobRecords(records, jobMonitorName).All(IsTerminal);

        public static bool HasFailedNonMonitorJobs(IEnumerable<AzureDevOpsTimelineRecord> records, string jobMonitorName)
            => GetRelevantJobRecords(records, jobMonitorName).Any(r =>
                string.Equals(r.Result, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.Result, "canceled", StringComparison.OrdinalIgnoreCase));

        private static IEnumerable<AzureDevOpsTimelineRecord> GetRelevantJobRecords(IEnumerable<AzureDevOpsTimelineRecord> records, string jobMonitorName)
        {
            return (records ?? [])
                .Where(r => string.Equals(r.Type, "Job", StringComparison.OrdinalIgnoreCase))
                .Where(r => !string.Equals(r.Name, jobMonitorName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsTerminal(AzureDevOpsTimelineRecord record)
            => string.Equals(record?.State, "completed", StringComparison.OrdinalIgnoreCase);
    }
}
