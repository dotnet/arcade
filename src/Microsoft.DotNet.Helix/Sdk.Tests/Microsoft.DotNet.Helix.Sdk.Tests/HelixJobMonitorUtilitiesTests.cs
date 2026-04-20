// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Helix.JobMonitor;
using System;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class HelixJobMonitorUtilitiesTests
    {
        [Theory]
        [InlineData("https://github.com/dotnet/arcade", "dotnet/arcade")]
        [InlineData("https://dev.azure.com/dnceng/internal/_git/dotnet-arcade", "dotnet/arcade")]
        [InlineData("dotnet/arcade", "dotnet/arcade")]
        public void NormalizeRepository_ReturnsStableIdentifier(string input, string expected)
        {
            Assert.Equal(expected, HelixJobMonitorUtilities.NormalizeRepository(input));
        }

        [Fact]
        public void AreNonMonitorJobsComplete_IgnoresMonitorRecord()
        {
            var records = new[]
            {
                new AzureDevOpsTimelineRecord { Type = "Job", Name = "Build Linux", State = "completed", Result = "succeeded" },
                new AzureDevOpsTimelineRecord { Type = "Job", Name = "Helix Job Monitor", State = "inProgress", Result = null },
            };

            Assert.True(HelixJobMonitorUtilities.AreNonMonitorJobsComplete(records, "Helix Job Monitor"));
        }

        [Fact]
        public void HasFailedNonMonitorJobs_DetectsFailures()
        {
            var records = new[]
            {
                new AzureDevOpsTimelineRecord { Type = "Job", Name = "Build Linux", State = "completed", Result = "failed" },
                new AzureDevOpsTimelineRecord { Type = "Job", Name = "Helix Job Monitor", State = "inProgress", Result = null },
            };

            Assert.True(HelixJobMonitorUtilities.HasFailedNonMonitorJobs(records, "Helix Job Monitor"));
        }

        [Fact]
        public void GetTestRunName_ProducesStableName()
        {
            Assert.Equal(
                "Helix Job Monitor - coreclr-tests-linux-x64",
                HelixJobMonitorUtilities.GetTestRunName("coreclr-tests-linux-x64"));
        }
    }
}
