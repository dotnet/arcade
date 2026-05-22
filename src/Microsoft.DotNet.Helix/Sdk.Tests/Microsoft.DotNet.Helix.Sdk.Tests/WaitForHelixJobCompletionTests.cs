// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Helix.Client.Models;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class WaitForHelixJobCompletionTests
    {
        [Fact]
        public void GetQueuedAndInProgressWorkItemsClassifiesExpectedStates()
        {
            IReadOnlyList<WorkItemSummary> workItems = new List<WorkItemSummary>
            {
                new WorkItemSummary("details-1", "job-1", "queued-item", "Waiting"),
                new WorkItemSummary("details-2", "job-1", "running-item", "Running"),
                new WorkItemSummary("details-3", "job-1", "pending-item", "Pending"),
                new WorkItemSummary("details-4", "job-1", "passed-item", "Passed"),
                new WorkItemSummary("details-5", "job-1", "failed-item", "Failed")
            };

            var result = WaitForHelixJobCompletion.GetQueuedAndInProgressWorkItems(workItems);

            Assert.Equal(2, result.queuedWorkItems.Count);
            Assert.Contains("queued-item (Waiting)", result.queuedWorkItems);
            Assert.Contains("pending-item (Pending)", result.queuedWorkItems);

            Assert.Single(result.inProgressWorkItems);
            Assert.Contains("running-item (Running)", result.inProgressWorkItems);
        }

        [Fact]
        public void GetQueuedAndInProgressWorkItemsHandlesNullEntries()
        {
            IReadOnlyList<WorkItemSummary> workItems = new List<WorkItemSummary>
            {
                new WorkItemSummary("details-1", "job-1", null!, "Queued"),
                new WorkItemSummary("details-2", "job-1", "running-item", null!)
            };

            var result = WaitForHelixJobCompletion.GetQueuedAndInProgressWorkItems(workItems);

            Assert.Single(result.queuedWorkItems);
            Assert.Contains("<unnamed work item> (Queued)", result.queuedWorkItems);
            Assert.Empty(result.inProgressWorkItems);
        }
    }
}
