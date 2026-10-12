// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
}
