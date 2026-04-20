// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Helix.Reporter;
using System;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class HelixReporterJobUtilitiesTests
    {
        [Theory]
        [InlineData("https://github.com/dotnet/arcade", "dotnet/arcade")]
        [InlineData("https://dev.azure.com/dnceng/internal/_git/dotnet-arcade", "dotnet/arcade")]
        [InlineData("dotnet/arcade", "dotnet/arcade")]
        public void NormalizeRepository_ReturnsStableIdentifier(string input, string expected)
        {
            Assert.Equal(expected, HelixReporterJobUtilities.NormalizeRepository(input));
        }

        [Fact]
        public void AreNonReporterJobsComplete_IgnoresReporterRecord()
        {
            var records = new[]
            {
                new AzureDevOpsTimelineRecord { Type = "Job", Name = "Build Linux", State = "completed", Result = "succeeded" },
                new AzureDevOpsTimelineRecord { Type = "Job", Name = "Helix Reporter", State = "inProgress", Result = null },
            };

            Assert.True(HelixReporterJobUtilities.AreNonReporterJobsComplete(records, "Helix Reporter"));
        }

        [Fact]
        public void HasFailedNonReporterJobs_DetectsFailures()
        {
            var records = new[]
            {
                new AzureDevOpsTimelineRecord { Type = "Job", Name = "Build Linux", State = "completed", Result = "failed" },
                new AzureDevOpsTimelineRecord { Type = "Job", Name = "Helix Reporter", State = "inProgress", Result = null },
            };

            Assert.True(HelixReporterJobUtilities.HasFailedNonReporterJobs(records, "Helix Reporter"));
        }

        [Fact]
        public void GetTestRunName_ProducesStableName()
        {
            Assert.Equal(
                "Helix Reporter - coreclr-tests-linux-x64",
                HelixReporterJobUtilities.GetTestRunName("coreclr-tests-linux-x64"));
        }
    }
}
