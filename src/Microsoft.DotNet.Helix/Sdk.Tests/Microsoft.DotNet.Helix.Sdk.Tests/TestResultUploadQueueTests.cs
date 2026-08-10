// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.Helix.JobMonitor;
using Microsoft.DotNet.Helix.JobMonitor.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class TestResultUploadQueueTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        [Fact]
        public async Task PreparationParallelismIsBounded()
        {
            int active = 0;
            int maximum = 0;
            var maximumReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var helix = new PipelineHelixService
            {
                DownloadAsync = async (context, workItemName, cancellationToken) =>
                {
                    int current = Interlocked.Increment(ref active);
                    UpdateMaximum(ref maximum, current);
                    if (current == 4)
                    {
                        maximumReached.TrySetResult();
                    }

                    await release.Task.WaitAsync(Timeout, cancellationToken);
                    Interlocked.Decrement(ref active);
                    return Result(context.JobName, workItemName);
                },
            };
            var azdo = new PipelineAzureDevOpsService();
            var state = new MonitorState();
            using var queue = CreateQueue(helix, azdo, state, processingParallelism: 4, uploadParallelism: 8);

            await EnqueueAsync(queue, state, Job("job"), WorkItems("job", 12));
            await maximumReached.Task.WaitAsync(Timeout);

            maximum.Should().Be(4);
            release.TrySetResult();
            await queue.DrainAsync(CancellationToken.None).WaitAsync(Timeout);
            state.IsHelixJobProcessed("job").Should().BeTrue();
        }

        [Fact]
        public async Task PublicationParallelismIsIndependentAndBounded()
        {
            int active = 0;
            int maximum = 0;
            var maximumReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var helix = new PipelineHelixService();
            var azdo = new PipelineAzureDevOpsService
            {
                PublishAsync = async (_, prepared, cancellationToken) =>
                {
                    int current = Interlocked.Increment(ref active);
                    UpdateMaximum(ref maximum, current);
                    if (current == 8)
                    {
                        maximumReached.TrySetResult();
                    }

                    await release.Task.WaitAsync(Timeout, cancellationToken);
                    Interlocked.Decrement(ref active);
                    return new TestResultUploadSummary(true, prepared.WorkItem.TestResultFiles.Count);
                },
            };
            var state = new MonitorState();
            using var queue = CreateQueue(helix, azdo, state, processingParallelism: 4, uploadParallelism: 8);

            await EnqueueAsync(queue, state, Job("job"), WorkItems("job", 20));
            await maximumReached.Task.WaitAsync(Timeout);

            maximum.Should().Be(8);
            release.TrySetResult();
            await queue.DrainAsync(CancellationToken.None).WaitAsync(Timeout);
            azdo.CreateTestRunCallCount.Should().Be(1);
        }

        [Fact]
        public async Task SlowPublicationDoesNotHoldPreparationSlots()
        {
            int preparedCount = 0;
            var threePrepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var publicationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var publicationRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var helix = new PipelineHelixService();
            var azdo = new PipelineAzureDevOpsService
            {
                PrepareAsync = (result, _) =>
                {
                    if (Interlocked.Increment(ref preparedCount) >= 3)
                    {
                        threePrepared.TrySetResult();
                    }

                    return Task.FromResult(new PreparedWorkItemTestResults(result, PreparedResults: null));
                },
                PublishAsync = async (_, prepared, cancellationToken) =>
                {
                    publicationStarted.TrySetResult();
                    await publicationRelease.Task.WaitAsync(Timeout, cancellationToken);
                    return new TestResultUploadSummary(true, prepared.WorkItem.TestResultFiles.Count);
                },
            };
            var state = new MonitorState();
            using var queue = CreateQueue(helix, azdo, state, processingParallelism: 2, uploadParallelism: 1);

            await EnqueueAsync(queue, state, Job("job"), WorkItems("job", 8));
            await publicationStarted.Task.WaitAsync(Timeout);
            await threePrepared.Task.WaitAsync(Timeout);

            preparedCount.Should().BeGreaterThanOrEqualTo(3);
            publicationRelease.TrySetResult();
            await queue.DrainAsync(CancellationToken.None).WaitAsync(Timeout);
        }

        [Fact]
        public async Task SchedulingIsFairAndBackpressuredAcrossJobs()
        {
            var preparationOrder = new ConcurrentQueue<string>();
            int preparationCount = 0;
            var firstPreparationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstPreparationRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thirdPreparationReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondJobSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var publicationPermits = new SemaphoreSlim(0);

            var helix = new PipelineHelixService
            {
                DownloadAsync = async (context, workItemName, cancellationToken) =>
                {
                    string key = $"{context.JobName}/{workItemName}";
                    preparationOrder.Enqueue(key);
                    int count = Interlocked.Increment(ref preparationCount);
                    if (count == 1)
                    {
                        firstPreparationStarted.TrySetResult();
                        await firstPreparationRelease.Task.WaitAsync(Timeout, cancellationToken);
                    }
                    if (count >= 3)
                    {
                        thirdPreparationReached.TrySetResult();
                    }
                    if (context.JobName == "small")
                    {
                        secondJobSeen.TrySetResult();
                    }

                    return Result(context.JobName, workItemName);
                },
            };
            var azdo = new PipelineAzureDevOpsService
            {
                PublishAsync = async (_, prepared, cancellationToken) =>
                {
                    await publicationPermits.WaitAsync(cancellationToken);
                    return new TestResultUploadSummary(true, prepared.WorkItem.TestResultFiles.Count);
                },
            };
            var state = new MonitorState();
            using var queue = CreateQueue(helix, azdo, state, processingParallelism: 1, uploadParallelism: 1);

            await EnqueueAsync(queue, state, Job("large"), WorkItems("large", 20));
            await firstPreparationStarted.Task.WaitAsync(Timeout);
            await EnqueueAsync(queue, state, Job("small"), WorkItems("small", 1));
            firstPreparationRelease.TrySetResult();

            await thirdPreparationReached.Task.WaitAsync(Timeout);
            await WaitUntilAsync(() =>
            {
                (int activePreparations, int queuedPublications, int activePublications) =
                    queue.SnapshotOccupancy();
                return activePreparations == 1
                    && queuedPublications == 2
                    && activePublications == 1;
            });
            preparationCount.Should().Be(3, "the bounded publication queue must backpressure preparation");

            for (int i = 0; i < 4 && !secondJobSeen.Task.IsCompleted; i++)
            {
                int previousCount = Volatile.Read(ref preparationCount);
                publicationPermits.Release();
                await WaitUntilAsync(() =>
                    secondJobSeen.Task.IsCompleted
                    || Volatile.Read(ref preparationCount) > previousCount);
            }

            await secondJobSeen.Task.WaitAsync(Timeout);
            preparationOrder.ToArray().Take(6).Should().Contain(item => item.StartsWith("small/", StringComparison.Ordinal));

            publicationPermits.Release(32);
            await queue.DrainAsync(CancellationToken.None).WaitAsync(Timeout);
        }

        [Fact]
        public async Task SlowJobContextDoesNotMonopolizeProcessing()
        {
            var slowContextStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var slowContextRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var smallJobPrepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var helix = new PipelineHelixService
            {
                ContextAsync = async (jobName, workingDirectory, cancellationToken) =>
                {
                    if (jobName == "large")
                    {
                        slowContextStarted.TrySetResult();
                        await slowContextRelease.Task.WaitAsync(Timeout, cancellationToken);
                    }

                    return new HelixTestResultsContext(jobName, workingDirectory, ResultsSas: "?sas");
                },
                DownloadAsync = (context, workItemName, _) =>
                {
                    if (context.JobName == "small")
                    {
                        smallJobPrepared.TrySetResult();
                    }

                    return Task.FromResult(Result(context.JobName, workItemName));
                },
            };
            var azdo = new PipelineAzureDevOpsService();
            var state = new MonitorState();
            using var queue = CreateQueue(helix, azdo, state, processingParallelism: 2, uploadParallelism: 1);

            await EnqueueAsync(queue, state, Job("large"), WorkItems("large", 20));
            await slowContextStarted.Task.WaitAsync(Timeout);
            await EnqueueAsync(queue, state, Job("small"), WorkItems("small", 1));

            await smallJobPrepared.Task.WaitAsync(Timeout);
            await WaitUntilAsync(() => state.IsHelixJobProcessed("small"));
            state.IsHelixJobProcessed("small").Should().BeTrue();

            slowContextRelease.TrySetResult();
            await queue.DrainAsync(CancellationToken.None).WaitAsync(Timeout);
        }

        [Fact]
        public async Task TransientRetryOnlyRedownloadsFailedWorkItem()
        {
            var downloadCounts = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var helix = new PipelineHelixService
            {
                DownloadAsync = (context, workItemName, _) =>
                {
                    int attempt = downloadCounts.AddOrUpdate(workItemName, 1, static (_, count) => count + 1);
                    if (workItemName == "retry" && attempt == 1)
                    {
                        throw new HttpRequestException(
                            "Injected transient failure.",
                            null,
                            HttpStatusCode.ServiceUnavailable);
                    }

                    return Task.FromResult(Result(context.JobName, workItemName));
                },
            };
            var azdo = new PipelineAzureDevOpsService();
            var state = new MonitorState();
            using var queue = CreateQueue(helix, azdo, state, processingParallelism: 1, uploadParallelism: 1);

            await EnqueueAsync(
                queue,
                state,
                Job("job"),
                [WorkItem("job", "stable"), WorkItem("job", "retry")]);
            await queue.DrainAsync(CancellationToken.None).WaitAsync(Timeout);

            downloadCounts["stable"].Should().Be(1);
            downloadCounts["retry"].Should().Be(2);
            helix.ContextCounts["job"].Should().Be(1);
        }

        [Fact]
        public async Task FailedJobDoesNotDisruptUnrelatedJob()
        {
            var helix = new PipelineHelixService();
            var azdo = new PipelineAzureDevOpsService
            {
                PublishAsync = (_, prepared, _) =>
                {
                    if (prepared.WorkItem.JobName == "bad")
                    {
                        throw new InvalidOperationException("Injected publication failure.");
                    }

                    return Task.FromResult(new TestResultUploadSummary(true, 1));
                },
            };
            var state = new MonitorState();
            using var queue = CreateQueue(helix, azdo, state, processingParallelism: 2, uploadParallelism: 2);

            await EnqueueAsync(queue, state, Job("bad"), WorkItems("bad", 1));
            await EnqueueAsync(queue, state, Job("good"), WorkItems("good", 1));
            await queue.DrainAsync(CancellationToken.None).WaitAsync(Timeout);

            state.IsHelixJobProcessed("bad").Should().BeFalse();
            state.IsHelixJobProcessed("good").Should().BeTrue();
            azdo.CompletedJobs.Should().BeEquivalentTo(["good"]);
        }

        [Fact]
        public async Task TestRunCompletesOnlyAfterEveryWorkItemPublishes()
        {
            var events = new ConcurrentQueue<string>();
            var slowPublicationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var slowPublicationRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var helix = new PipelineHelixService();
            var azdo = new PipelineAzureDevOpsService
            {
                PublishAsync = async (_, prepared, cancellationToken) =>
                {
                    string workItemName = prepared.WorkItem.WorkItemName;
                    events.Enqueue($"publish-start:{workItemName}");
                    if (workItemName == "slow")
                    {
                        slowPublicationStarted.TrySetResult();
                        await slowPublicationRelease.Task.WaitAsync(Timeout, cancellationToken);
                    }
                    events.Enqueue($"publish-end:{workItemName}");
                    return new TestResultUploadSummary(true, 1);
                },
                CompleteAsync = (_, jobName, _, _) =>
                {
                    events.Enqueue($"complete:{jobName}");
                    return Task.CompletedTask;
                },
            };
            var state = new MonitorState();
            using var queue = CreateQueue(helix, azdo, state, processingParallelism: 2, uploadParallelism: 2);

            await EnqueueAsync(
                queue,
                state,
                Job("job"),
                [WorkItem("job", "fast"), WorkItem("job", "slow")]);
            await slowPublicationStarted.Task.WaitAsync(Timeout);

            events.Should().NotContain("complete:job");
            slowPublicationRelease.TrySetResult();
            await queue.DrainAsync(CancellationToken.None).WaitAsync(Timeout);

            events.Last().Should().Be("complete:job");
        }

        [Fact]
        public async Task NormalDrainWaitsAndCanceledDrainDoesNotStopPipeline()
        {
            var publicationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var publicationRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var helix = new PipelineHelixService();
            var azdo = new PipelineAzureDevOpsService
            {
                PublishAsync = async (_, prepared, _) =>
                {
                    publicationStarted.TrySetResult();
                    await publicationRelease.Task.WaitAsync(Timeout);
                    return new TestResultUploadSummary(true, prepared.WorkItem.TestResultFiles.Count);
                },
            };
            var state = new MonitorState();
            using var queue = CreateQueue(helix, azdo, state, processingParallelism: 1, uploadParallelism: 1);

            await EnqueueAsync(queue, state, Job("job"), WorkItems("job", 1));
            Task normalDrain = queue.DrainAsync(CancellationToken.None);
            await publicationStarted.Task.WaitAsync(Timeout);
            normalDrain.IsCompleted.Should().BeFalse();

            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => queue.DrainAsync(canceled.Token));
            normalDrain.IsCompleted.Should().BeFalse();

            publicationRelease.TrySetResult();
            await normalDrain.WaitAsync(Timeout);
            state.IsHelixJobProcessed("job").Should().BeTrue();
        }

        private static TestResultUploadQueue CreateQueue(
            IHelixService helix,
            IAzureDevOpsService azdo,
            MonitorState state,
            int processingParallelism,
            int uploadParallelism)
            => new(
                NullLogger.Instance,
                new JobMonitorOptions
                {
                    TestResultProcessingParallelism = processingParallelism,
                    TestResultUploadParallelism = uploadParallelism,
                    WorkingDirectory = "work",
                },
                azdo,
                helix,
                state);

        private static async Task EnqueueAsync(
            TestResultUploadQueue queue,
            MonitorState state,
            HelixJobInfo job,
            IReadOnlyCollection<WorkItemSummary> workItems)
        {
            state.ObserveJobs([job]);
            state.TryQueueHelixJobUpload(job.JobName).Should().BeTrue();
            await queue.EnqueueAsync(job, workItems, CancellationToken.None);
        }

        private static HelixJobInfo Job(string jobName)
            => new(jobName, "finished", testRunName: $"{jobName} test run");

        private static WorkItemSummary[] WorkItems(string jobName, int count)
            => [..Enumerable.Range(0, count).Select(i => WorkItem(jobName, $"work-item-{i}"))];

        private static WorkItemSummary WorkItem(string jobName, string workItemName)
            => new($"{jobName}/{workItemName}", jobName, workItemName, "Finished") { ExitCode = 0 };

        private static WorkItemTestResults Result(string jobName, string workItemName)
            => new(jobName, workItemName, [$"{workItemName}.trx"]);

        private static void UpdateMaximum(ref int maximum, int candidate)
        {
            int observed;
            while (candidate > (observed = Volatile.Read(ref maximum)))
            {
                if (Interlocked.CompareExchange(ref maximum, candidate, observed) == observed)
                {
                    return;
                }
            }
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow + Timeout;
            while (!condition())
            {
                if (DateTimeOffset.UtcNow >= deadline)
                {
                    throw new TimeoutException("Timed out waiting for the expected pipeline state.");
                }

                await Task.Delay(10);
            }
        }

        private sealed class PipelineHelixService : IHelixService
        {
            public Func<string, string, CancellationToken, Task<HelixTestResultsContext>> ContextAsync { get; set; } =
                (jobName, workingDirectory, _) => Task.FromResult(
                    new HelixTestResultsContext(jobName, workingDirectory, ResultsSas: "?sas"));

            public Func<HelixTestResultsContext, string, CancellationToken, Task<WorkItemTestResults>> DownloadAsync { get; set; } =
                (context, workItemName, _) => Task.FromResult(Result(context.JobName, workItemName));

            public ConcurrentDictionary<string, int> ContextCounts { get; } =
                new(StringComparer.OrdinalIgnoreCase);

            public async Task<HelixTestResultsContext> CreateTestResultsContextAsync(
                string jobName,
                string workingDirectory,
                CancellationToken cancellationToken)
            {
                ContextCounts.AddOrUpdate(jobName, 1, static (_, count) => count + 1);
                return await ContextAsync(jobName, workingDirectory, cancellationToken);
            }

            public Task<WorkItemTestResults> DownloadTestResultsAsync(
                HelixTestResultsContext context,
                string workItemName,
                CancellationToken cancellationToken)
                => DownloadAsync(context, workItemName, cancellationToken);

            public Task<IReadOnlyList<HelixJobInfo>> GetJobsForBuildAsync(
                string source,
                string buildId,
                CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<HelixJobInfo>>([]);

            public Task<IReadOnlyCollection<WorkItemSummary>> ListWorkItemsAsync(
                string jobName,
                CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyCollection<WorkItemSummary>>([]);

            public Task CancelJobAsync(string jobName, CancellationToken cancellationToken)
                => Task.CompletedTask;

            public Task<HelixJobInfo> ResubmitWorkItemsAsync(
                HelixJobInfo originalJob,
                IReadOnlyCollection<WorkItemSummary> failedWorkItems,
                string targetStageAttempt,
                CancellationToken cancellationToken)
                => Task.FromResult<HelixJobInfo>(null);
        }

        private sealed class PipelineAzureDevOpsService : IAzureDevOpsService
        {
            private int _nextTestRunId;
            private int _createTestRunCallCount;

            public Func<WorkItemTestResults, CancellationToken, Task<PreparedWorkItemTestResults>> PrepareAsync { get; set; } =
                (result, _) => Task.FromResult(new PreparedWorkItemTestResults(result, PreparedResults: null));

            public Func<int, PreparedWorkItemTestResults, CancellationToken, Task<TestResultUploadSummary>> PublishAsync { get; set; } =
                (_, prepared, _) => Task.FromResult(
                    new TestResultUploadSummary(true, prepared.WorkItem.TestResultFiles.Count));

            public Func<int, string, IReadOnlyCollection<string>, CancellationToken, Task> CompleteAsync { get; set; } =
                (_, _, _, _) => Task.CompletedTask;

            public ConcurrentBag<string> CompletedJobs { get; } = [];

            public int CreateTestRunCallCount => Volatile.Read(ref _createTestRunCallCount);

            public Task<IReadOnlyList<AzureDevOpsTimelineRecord>> GetTimelineRecordsAsync(CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyList<AzureDevOpsTimelineRecord>>([]);

            public Task<IReadOnlySet<string>> GetProcessedHelixJobNamesAsync(CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

            public Task<IReadOnlyDictionary<string, IReadOnlySet<string>>> GetFailedTestWorkItemsAsync(
                CancellationToken cancellationToken)
                => Task.FromResult<IReadOnlyDictionary<string, IReadOnlySet<string>>>(
                    new Dictionary<string, IReadOnlySet<string>>());

            public Task<int> CreateTestRunAsync(string name, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _createTestRunCallCount);
                return Task.FromResult(Interlocked.Increment(ref _nextTestRunId));
            }

            public async Task CompleteTestRunAsync(
                int testRunId,
                string helixJobName,
                IReadOnlyCollection<string> failedWorkItems,
                CancellationToken cancellationToken)
            {
                await CompleteAsync(testRunId, helixJobName, failedWorkItems, cancellationToken);
                CompletedJobs.Add(helixJobName);
            }

            public Task<PreparedWorkItemTestResults> PrepareTestResultsAsync(
                WorkItemTestResults results,
                CancellationToken cancellationToken)
                => PrepareAsync(results, cancellationToken);

            public Task<TestResultUploadSummary> PublishTestResultsAsync(
                int testRunId,
                PreparedWorkItemTestResults results,
                CancellationToken cancellationToken)
                => PublishAsync(testRunId, results, cancellationToken);
        }
    }
}
