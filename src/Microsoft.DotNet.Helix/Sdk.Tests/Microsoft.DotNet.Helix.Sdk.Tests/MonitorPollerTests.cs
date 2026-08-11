// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.DotNet.Helix.JobMonitor;
using Microsoft.DotNet.Helix.JobMonitor.Models;
using Microsoft.DotNet.Helix.Sdk.Tests.Fakes;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class MonitorPollerTests
    {
        [Theory]
        [InlineData(null, false)]
        [InlineData("1", true)]
        [InlineData("2", false)]
        [InlineData("3", false)]
        public void IsPreviousAttempt_RequiresNumericallyLowerAttempt(string jobAttempt, bool expected)
        {
            var options = new JobMonitorOptions { StageAttempt = "2" };
            var poller = new MonitorPoller(
                options,
                new FakeAzureDevOpsService(),
                new FakeHelixService(),
                "source");
            var job = new HelixJobInfo("job", "running", stageAttempt: jobAttempt);

            poller.IsPreviousAttempt(job).Should().Be(expected);
        }

        [Fact]
        public async Task CaptureAsync_ReusesTerminalWorkItemsAcrossPolls()
        {
            var job = new HelixJobInfo("job", "finished", initialWorkItemCount: 1);
            var helix = new FakeHelixService()
                .AddResponse(
                    [job],
                    new Dictionary<string, HelixJobPassFail>
                    {
                        ["job"] = new(["item"], []),
                    });
            var poller = new MonitorPoller(
                new JobMonitorOptions { BuildId = "1" },
                new FakeAzureDevOpsService(),
                helix,
                "source");

            MonitorSnapshot first = await poller.CaptureAsync([job], CancellationToken.None);
            MonitorSnapshot second = await poller.CaptureAsync(null, CancellationToken.None);

            first.WorkItemsByJob["job"].Should().ContainSingle();
            second.WorkItemsByJob["job"].Should().ContainSingle();
            helix.GetListWorkItemsCallCount("job").Should().Be(1);
        }
    }
}
