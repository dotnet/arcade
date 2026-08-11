// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.DotNet.Helix.JobMonitor.ResultPublishing;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.Helix.JobMonitor.Models;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    /// <summary>One coherent poll: stage timeline, jobs, and exactly one work-item read per job.</summary>
    internal sealed record MonitorSnapshot(
        IReadOnlyList<AzureDevOpsTimelineRecord> TimelineRecords,
        IReadOnlyList<HelixJobInfo> StageJobs,
        IReadOnlyDictionary<string, IReadOnlyCollection<WorkItemSummary>> WorkItemsByJob,
        IReadOnlySet<string> CompletedJobNames);
}
