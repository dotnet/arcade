// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.DotNet.Helix.JobMonitor.ResultPublishing;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.Helix.JobMonitor;
using Microsoft.DotNet.Helix.JobMonitor.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class AsyncWorkQueueTests
    {
        [Fact]
        public async Task BoundedQueue_AppliesBackpressure_AndDrainsAcceptedWork()
        {
            var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var completed = new List<int>();
            using var queue = new AsyncWorkQueue<int>(1, 1, async (item, _) =>
            {
                if (item == 1)
                {
                    firstStarted.TrySetResult();
                    await release.Task;
                }

                completed.Add(item);
            });

            await queue.EnqueueAsync(1, CancellationToken.None);
            await firstStarted.Task;
            await queue.EnqueueAsync(2, CancellationToken.None);
            ValueTask third = queue.EnqueueAsync(3, CancellationToken.None);
            third.IsCompleted.Should().BeFalse();

            release.TrySetResult();
            await third;
            await queue.CompleteAndDrainAsync(CancellationToken.None);

            completed.Should().Equal(1, 2, 3);
        }

        [Fact]
        public async Task WorkerFault_IsReportedByDrain()
        {
            using var queue = new AsyncWorkQueue<int>(1, 1, (_, _) => throw new InvalidOperationException("boom"));
            await queue.EnqueueAsync(1, CancellationToken.None);

            Func<Task> drain = () => queue.CompleteAndDrainAsync(CancellationToken.None);
            await drain.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task SharedLimiter_BoundsConcurrencyAcrossIndependentCallers()
        {
            using var limiter = new AsyncConcurrencyLimiter(2);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int active = 0;
            int maximumActive = 0;

            async Task RunAsync()
            {
                await limiter.RunAsync(async _ =>
                {
                    int current = Interlocked.Increment(ref active);
                    InterlockedExtensions.Max(ref maximumActive, current);
                    await release.Task;
                    Interlocked.Decrement(ref active);
                    return true;
                }, CancellationToken.None);
            }

            Task[] operations = [RunAsync(), RunAsync(), RunAsync(), RunAsync()];
            await WaitForAsync(() => Volatile.Read(ref active) == 2);
            maximumActive.Should().Be(2);

            release.TrySetResult();
            await Task.WhenAll(operations);
            maximumActive.Should().Be(2);
        }

        [Fact]
        public async Task Abandon_CancelsInFlightUpload_WithoutDraining()
        {
            var helix = new BlockingDownloadHelixService();
            var options = new JobMonitorOptions
            {
                TestResultUploadParallelism = 1,
                WorkingDirectory = ".",
            };
            var ledger = new MonitorLedger();
            using var pipeline = new TestResultUploadPipeline(
                NullLogger.Instance, options, new NoOpAzureDevOpsService(), helix, ledger);
            var job = new HelixJobInfo("job", "finished");
            var item = new WorkItemSummary("job/item", "job", "item", "Finished") { ExitCode = 0 };

            await pipeline.EnqueueAsync(job, [item], CancellationToken.None);
            await helix.DownloadStarted.Task;
            pipeline.Abandon();

            await helix.DownloadCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            ledger.IsHelixJobProcessed(job.JobName).Should().BeFalse();
        }

        private sealed class BlockingDownloadHelixService : IHelixService
        {
            public TaskCompletionSource DownloadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource DownloadCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<IReadOnlyList<HelixJobInfo>> GetJobsForBuildAsync(string source, string buildId, CancellationToken cancellationToken) => throw new NotSupportedException();
            public async Task<IReadOnlyList<WorkItemTestResults>> DownloadTestResultsAsync(string jobName, IReadOnlyCollection<string> workItemNames, string workingDirectory, CancellationToken cancellationToken)
            {
                DownloadStarted.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
                catch (OperationCanceledException) { DownloadCancelled.TrySetResult(); throw; }
                return [];
            }
            public Task<IReadOnlyCollection<WorkItemSummary>> ListWorkItemsAsync(string jobName, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task CancelJobAsync(string jobName, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<HelixJobInfo> ResubmitWorkItemsAsync(HelixJobInfo originalJob, IReadOnlyCollection<WorkItemSummary> failedWorkItems, string targetStageAttempt, CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        private sealed class NoOpAzureDevOpsService : IAzureDevOpsService
        {
            public Task<IReadOnlyList<AzureDevOpsTimelineRecord>> GetTimelineRecordsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<IReadOnlySet<string>> GetProcessedHelixJobNamesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<IReadOnlyDictionary<string, IReadOnlySet<string>>> GetFailedTestWorkItemsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<int> CreateTestRunAsync(string name, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task CompleteTestRunAsync(int testRunId, string helixJobName, IReadOnlyCollection<string> failedWorkItems, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task<IReadOnlyDictionary<(string JobName, string WorkItemName), TestResultUploadSummary>> UploadTestResultsAsync(int testRunId, IReadOnlyList<WorkItemTestResults> results, CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        private static async Task WaitForAsync(Func<bool> condition)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!condition())
            {
                await Task.Delay(10, timeout.Token);
            }
        }

        private static class InterlockedExtensions
        {
            public static void Max(ref int location, int value)
            {
                int current;
                while ((current = Volatile.Read(ref location)) < value
                    && Interlocked.CompareExchange(ref location, value, current) != current)
                {
                }
            }
        }
    }
}
