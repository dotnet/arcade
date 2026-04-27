// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Helix.JobMonitor;
using Microsoft.DotNet.Helix.JobMonitor.Models;

namespace Microsoft.DotNet.Helix.Sdk.Tests.ScenarioHelpers
{
    internal static class ScenarioHelpers
    {
        public const string DefaultMonitorName = "Helix Job Monitor";

        public static AzureDevOpsTimelineRecord PipelineJob(string name, string state, string result = null)
            => new() { Type = "Job", Name = name, State = state, Result = result };

        public static AzureDevOpsTimelineRecord MonitorJob(string name = DefaultMonitorName)
            => new() { Type = "Job", Name = name, State = "inProgress" };

        public static HelixJobInfo HelixJob(string jobName, string status)
            => new(jobName, status);

        public static HelixJobPassFail PassFail(string[] passed = null, string[] failed = null)
            => new(passed ?? [], failed ?? []);
    }
}
