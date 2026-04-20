// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        public static string NormalizeRepository(string repository)
        {
            if (string.IsNullOrWhiteSpace(repository))
            {
                return string.Empty;
            }

            repository = repository.Trim().TrimEnd('/');
            if (!Uri.TryCreate(repository, UriKind.Absolute, out Uri uri))
            {
                return repository.Trim('/');
            }

            string[] segments = uri.AbsolutePath.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
            if (uri.Host.Contains("github.com", StringComparison.OrdinalIgnoreCase) && segments.Length >= 2)
            {
                return $"{segments[0]}/{segments[1]}";
            }

            int gitIndex = Array.FindIndex(segments, s => string.Equals(s, "_git", StringComparison.OrdinalIgnoreCase));
            if (gitIndex > 0 && segments.Length > gitIndex + 1)
            {
                string project = segments[gitIndex - 1];
                string repoName = segments[gitIndex + 1];
                if ((string.Equals(project, "internal", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(project, "public", StringComparison.OrdinalIgnoreCase))
                    && repoName.Contains('-', StringComparison.Ordinal))
                {
                    int separatorIndex = repoName.IndexOf('-', StringComparison.Ordinal);
                    return $"{repoName.Substring(0, separatorIndex)}/{repoName.Substring(separatorIndex + 1)}";
                }

                return $"{project}/{repoName}";
            }

            return repository;
        }

        public static bool AreNonMonitorJobsComplete(IEnumerable<AzureDevOpsTimelineRecord> records, string jobMonitorName)
            => GetRelevantJobRecords(records, jobMonitorName).All(IsTerminal);

        public static bool HasFailedNonMonitorJobs(IEnumerable<AzureDevOpsTimelineRecord> records, string jobMonitorName)
            => GetRelevantJobRecords(records, jobMonitorName).Any(r =>
                string.Equals(r.Result, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(r.Result, "canceled", StringComparison.OrdinalIgnoreCase));

        public static string GetTestRunName(string helixJobName)
            => $"Helix Job Monitor - {helixJobName}";

        public static string CleanWorkItemName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            if (!name.Contains('%'))
            {
                name = WebUtility.UrlDecode(name);
            }

            return name.Replace('/', '-').Replace('\\', '-');
        }

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
