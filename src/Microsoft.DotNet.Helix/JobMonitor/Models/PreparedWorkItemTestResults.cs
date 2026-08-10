// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    public sealed record PreparedWorkItemTestResults(
        WorkItemTestResults WorkItem,
        AzureDevOpsResultPublisher.PreparedTestResults PreparedResults);
}
