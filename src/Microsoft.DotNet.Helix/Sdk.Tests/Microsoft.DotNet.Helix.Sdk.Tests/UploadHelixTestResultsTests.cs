// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using TaskItem = Microsoft.Build.Utilities.TaskItem;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.DotNet.Helix.Sdk.Tests.Fakes;
using Moq;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests;

public class UploadHelixTestResultsTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 4)]
    public async Task UploadAsync_LogsConciseSummaryAndDetailedStages(int fileCount, int resultCount)
    {
        var task = CreateTask(WorkItem("job", "work-item", "101", failed: false));
        ConcurrentQueue<BuildMessageEventArgs> messages = CaptureMessages(task);
        string downloadSummary = fileCount == 0
            ? "No supported test-result files found for 'job/work-item'."
            : $"Downloaded {fileCount} test-result file(s) for 'job/work-item'. Parsing and aggregating results.";
        string preparationSummary =
            $"Prepared {resultCount} top-level test result(s) from {fileCount} file(s) for 'job/work-item'. Publishing to Azure Pipelines test run 101.";
        var processor = new DelegateResultProcessor((results, ct) =>
        {
            Assert.Contains(messages, message => message.Message == downloadSummary);
            return Task.FromResult(new PreparedTestResults(
                [.. Enumerable.Range(0, resultCount).Select(i => new AggregatedResult(AggregationType.Single, $"Test{i}", 1,
                    i == 0 ? "Failed" : i == 1 ? "NotExecuted" : "Passed"))],
                AllPassed: resultCount == 0));
        });
        var publisher = new DelegateResultPublisher((runId, results, prepared, ct) =>
        {
            Assert.Contains(messages, message => message.Message == preparationSummary);
            return Task.FromResult((long)resultCount);
        });

        await task.UploadAsync(
            (item, ct) =>
            {
                Assert.Contains(messages, message => message.Message ==
                    "Downloading test-result files for 'job/work-item' (Azure Pipelines test run 101).");
                return Task.FromResult(new WorkItemTestResults("job", "work-item",
                    [.. Enumerable.Range(0, fileCount).Select(i => $"results-{i}.trx")]));
            }, processor, publisher, CancellationToken.None);

        Assert.Contains(messages, message => message.Message ==
            "Processing Helix test results for 1 completed work item(s), with up to 16 work items in parallel.");
        string outcomes = resultCount == 0 ? "" : " (1 Failed, 1 NotExecuted, 2 Passed)";
        Assert.Contains(messages, message => message.Message ==
            $"Work item 'work-item' of Helix job 'job': {resultCount} test result(s) published{outcomes}. Completed 1/1 work item(s).");
        Assert.Equal(
            $"Finished processing Helix test results: 1 succeeded, 0 failed; {resultCount} test result(s) published from 1 work item(s).",
            messages.Last().Message);
        Assert.Equal(3, messages.Count(message => message.Importance == MessageImportance.High));
        Assert.Equal(
            new[]
            {
                "Downloading test-result files for 'job/work-item' (Azure Pipelines test run 101).",
                downloadSummary,
                preparationSummary,
            },
            messages.Where(message => message.Importance == MessageImportance.Low).Select(message => message.Message));
    }

    [Fact]
    public async Task UploadAsync_PublishesPassedAndFailedWorkItemsIntoTheirOwnRuns()
    {
        var reporter = new FakeAzureDevOpsService().WithFailedTestResults("job-b", "same-name");
        var task = CreateTask(
            WorkItem("job-a", "same-name", "101", failed: false),
            WorkItem("job-b", "same-name", "202", failed: true));

        await task.UploadAsync(DownloadAsync, reporter, reporter, CancellationToken.None);

        Assert.Equal("job-a", Assert.Single(reporter.UploadedResultsByRunId[101]).JobName);
        Assert.Equal("job-b", Assert.Single(reporter.UploadedResultsByRunId[202]).JobName);
        Assert.True(reporter.PublishedPreparedResults[("job-a", "same-name")].AllPassed);
        Assert.False(reporter.PublishedPreparedResults[("job-b", "same-name")].AllPassed);
        Assert.Equal(2, reporter.PublishTestResultsCallCount);
        Assert.Equal(0, reporter.CreateTestRunCallCount);
        Assert.Equal(0, reporter.CompleteTestRunCallCount);
    }

    [Fact]
    public async Task UploadAsync_DoesNotCreateAdditionalSyntheticResultsWhenFilesAreMissing()
    {
        var reporter = new FakeAzureDevOpsService();
        var task = CreateTask(WorkItem("job", "crashed", "101", failed: true));

        await task.UploadAsync(
            (_, _) => Task.FromResult(new WorkItemTestResults("job", "crashed", [])),
            reporter, reporter, CancellationToken.None);

        Assert.Empty(Assert.Single(reporter.UploadedResultsByRunId[101]).TestResultFiles);
        Assert.Empty(reporter.PublishedPreparedResults[("job", "crashed")].Results);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task UploadAsync_RejectsInvalidRunIdsBeforeDownloading(string testRunId)
    {
        var reporter = new FakeAzureDevOpsService();
        var task = CreateTask(WorkItem("job", "work-item", testRunId, failed: false));
        bool downloaded = false;

        await Assert.ThrowsAsync<ArgumentException>(() => task.UploadAsync(
            (item, ct) =>
            {
                downloaded = true;
                return DownloadAsync(item, ct);
            }, reporter, reporter, CancellationToken.None));

        Assert.False(downloaded);
        Assert.Equal(0, reporter.PublishTestResultsCallCount);
    }

    [Fact]
    public async Task UploadAsync_RetriesTransientDownloadsWithoutRepublishing()
    {
        var reporter = new FakeAzureDevOpsService();
        var task = CreateTask(WorkItem("job", "work-item", "101", failed: false));
        int attempts = 0;

        await task.UploadAsync(
            (item, ct) => ++attempts == 1
                ? Task.FromException<WorkItemTestResults>(new HttpRequestException("Temporary download failure"))
                : DownloadAsync(item, ct),
            reporter, reporter, CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Equal(1, reporter.PublishTestResultsCallCount);
    }

    [Fact]
    public async Task UploadAsync_PropagatesPermanentDownloadAndPublishingFailures()
    {
        var reporter = new FakeAzureDevOpsService();
        var task = CreateTask(WorkItem("job", "work-item", "101", failed: false));
        var failure = new InvalidOperationException("Download failed");

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => task.UploadAsync(
            (_, _) => Task.FromException<WorkItemTestResults>(failure),
            reporter, reporter, CancellationToken.None)));
        Assert.Equal(0, reporter.PublishTestResultsCallCount);

        var publishingFailure = new InvalidOperationException("Publishing failed");
        reporter.FailNextUpload(publishingFailure);
        Assert.Same(publishingFailure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            task.UploadAsync(DownloadAsync, reporter, reporter, CancellationToken.None)));
        Assert.Empty(reporter.UploadedResultsByRunId);
    }

    [Theory]
    [InlineData("download")]
    [InlineData("parse")]
    [InlineData("publish")]
    public async Task UploadAsync_FinishesHealthyWorkItemsBeforeReportingFailures(string failingStage)
    {
        var releaseUploads = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var initialUploadsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failureInjected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reporter = new FakeAzureDevOpsService { UploadBlocker = releaseUploads.Task };
        var task = CreateTask([.. Enumerable.Range(0, 40).Select(i => WorkItem("job", $"work-{i}", "101", false))]);
        ConcurrentQueue<BuildMessageEventArgs> messages = CaptureMessages(task);
        var failures = new Dictionary<string, Exception>
        {
            ["work-0"] = new InvalidOperationException("First reporting failure"),
            ["work-32"] = new InvalidOperationException("Queued reporting failure"),
        };
        int uploadsStarted = 0;

        async Task<T> FailAsync<T>(string name, CancellationToken ct)
        {
            if (name == "work-0")
            {
                await initialUploadsStarted.Task.WaitAsync(ct);
                failureInjected.TrySetResult();
            }
            throw failures[name];
        }

        var processor = new DelegateResultProcessor((results, ct) =>
            failingStage == "parse" && failures.ContainsKey(results.WorkItemName)
                ? FailAsync<PreparedTestResults>(results.WorkItemName, ct)
                : reporter.PrepareAsync(results, ct));
        var publisher = new DelegateResultPublisher((runId, results, prepared, ct) =>
        {
            if (failingStage == "publish" && failures.ContainsKey(results.WorkItemName))
            {
                return FailAsync<long>(results.WorkItemName, ct);
            }
            if (Interlocked.Increment(ref uploadsStarted) == 15)
            {
                initialUploadsStarted.TrySetResult();
            }
            return reporter.PublishAsync(runId, results, prepared, ct);
        });

        Task upload = task.UploadAsync(
            (item, ct) => failingStage == "download" && failures.ContainsKey(item.GetMetadata("WorkItemName"))
                ? FailAsync<WorkItemTestResults>(item.GetMetadata("WorkItemName"), ct)
                : DownloadAsync(item, ct),
            processor, publisher, CancellationToken.None);
        try
        {
            await failureInjected.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(upload.IsCompleted);
        }
        finally
        {
            releaseUploads.TrySetResult();
        }

        AggregateException failure = await Assert.ThrowsAsync<AggregateException>(() => upload);
        Assert.Equal(2, failure.InnerExceptions.Count);
        Assert.All(failures.Values, expected => Assert.Contains(expected, failure.InnerExceptions));
        Assert.Equal(38, reporter.PublishTestResultsCallCount);
        Assert.Equal(
            Enumerable.Range(0, 40).Select(i => $"work-{i}").Where(name => !failures.ContainsKey(name)).Order(StringComparer.Ordinal),
            reporter.UploadedResultsByRunId[101].Select(result => result.WorkItemName).Order(StringComparer.Ordinal));
        Assert.InRange(reporter.MaximumConcurrentUploads, 1, 16);
        Assert.Equal(
            "Finished processing Helix test results: 38 succeeded, 2 failed; 38 test result(s) published from 40 work item(s).",
            messages.Last().Message);
    }

    [Fact]
    public async Task UploadAsync_CallerCancellationStopsBatchDespiteWorkItemFailures()
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reporter = new FakeAzureDevOpsService
        {
            UploadBlocker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task,
        };
        var task = CreateTask([.. Enumerable.Range(0, 40).Select(i => WorkItem("job", $"work-{i}", "101", false))]);
        int healthyDownloads = 0;
        Task upload = task.UploadAsync(
            (item, ct) =>
            {
                if (item.GetMetadata("WorkItemName") == "work-0")
                {
                    return Task.FromException<WorkItemTestResults>(new InvalidOperationException("Reporting failed"));
                }
                if (Interlocked.Increment(ref healthyDownloads) == 16)
                {
                    started.TrySetResult();
                }
                return DownloadAsync(item, ct);
            }, reporter, reporter, cancellation.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            cancellation.Cancel();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload);
        Assert.Equal(16, Volatile.Read(ref healthyDownloads));
        Assert.Equal(0, reporter.PublishTestResultsCallCount);
        Assert.Empty(reporter.UploadedResultsByRunId);
    }

    [Fact]
    public async Task UploadAsync_BoundsParallelismAndWaitsForAllUploads()
    {
        var releaseUploads = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reporter = new FakeAzureDevOpsService { UploadBlocker = releaseUploads.Task };
        var task = CreateTask([.. Enumerable.Range(0, 40).Select(i => WorkItem("job", $"work-{i}", "101", false))]);
        int downloads = 0;

        Task upload = task.UploadAsync(
            (item, ct) =>
            {
                if (Interlocked.Increment(ref downloads) == 16)
                {
                    started.TrySetResult();
                }
                return DownloadAsync(item, ct);
            }, reporter, reporter, CancellationToken.None);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(upload.IsCompleted);
            Assert.Equal(16, Volatile.Read(ref downloads));
            Assert.InRange(reporter.MaximumConcurrentUploads, 1, 16);
        }
        finally
        {
            releaseUploads.TrySetResult();
            await upload;
        }

        Assert.Equal(40, reporter.UploadedResultsByRunId[101].Count);
    }

    [Fact]
    public async Task UploadAsync_PropagatesCancellation()
    {
        var reporter = new FakeAzureDevOpsService();
        var task = CreateTask(WorkItem("job", "work-item", "101", failed: false));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            task.UploadAsync(DownloadAsync, reporter, reporter, cancellation.Token));
        Assert.Equal(0, reporter.PublishTestResultsCallCount);
    }

    private static UploadHelixTestResults CreateTask(params ITaskItem[] workItems)
        => new() { WorkItems = workItems, BuildEngine = new Mock<IBuildEngine>().Object };

    private static ConcurrentQueue<BuildMessageEventArgs> CaptureMessages(UploadHelixTestResults task)
    {
        var messages = new ConcurrentQueue<BuildMessageEventArgs>();
        var engine = new Mock<IBuildEngine>();
        engine.Setup(e => e.LogMessageEvent(It.IsAny<BuildMessageEventArgs>()))
            .Callback<BuildMessageEventArgs>(messages.Enqueue);
        task.BuildEngine = engine.Object;
        return messages;
    }

    private static ITaskItem WorkItem(string job, string name, string runId, bool failed)
    {
        var item = new TaskItem($"{job}/{name}");
        item.SetMetadata("JobName", job);
        item.SetMetadata("WorkItemName", name);
        item.SetMetadata("TestRunId", runId);
        item.SetMetadata("Failed", failed.ToString());
        return item;
    }

    private static Task<WorkItemTestResults> DownloadAsync(ITaskItem item, CancellationToken cancellationToken)
        => Task.FromResult(new WorkItemTestResults(
            item.GetMetadata("JobName"), item.GetMetadata("WorkItemName"), ["testResults.xml"]));

    private sealed class DelegateResultProcessor(
        Func<WorkItemTestResults, CancellationToken, Task<PreparedTestResults>> prepare) : ITestResultProcessor
    {
        public Task<PreparedTestResults> PrepareAsync(WorkItemTestResults results, CancellationToken cancellationToken)
            => prepare(results, cancellationToken);
    }

    private sealed class DelegateResultPublisher(
        Func<int, WorkItemTestResults, PreparedTestResults, CancellationToken, Task<long>> publish) : IAzureDevOpsResultPublisher
    {
        public Task<long> PublishAsync(
            int testRunId, WorkItemTestResults results, PreparedTestResults prepared, CancellationToken cancellationToken)
            => publish(testRunId, results, prepared, cancellationToken);
    }
}
