// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Microsoft.Arcade.Common;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.Helix.JobMonitor.Models;
using Microsoft.DotNet.Helix.JobMonitor.Parallelism;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Helix.JobMonitor;

internal sealed class TestResultUploadPipeline : IAsyncDisposable
{
    private const int MaximumTransientDownloadRetries = 2;
    private const string AzdoWarningPrefix = "##vso[task.logissue type=warning]";

    private readonly ILogger _logger;
    private readonly JobMonitorOptions _options;
    private readonly IAzureDevOpsService _azdo;
    private readonly IHelixService _helix;
    private readonly MonitorState _state;
    private readonly ConcurrentDictionary<string, JobUploadSession> _sessions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ActionQueue<JobUploadRequest> _jobs;
    private readonly ActionQueue<WorkItemUploadRequest> _workItems;
    private readonly ActionQueue<JobUploadSession> _finalizers;
    private int _draining;

    public TestResultUploadPipeline(
        ILogger logger,
        JobMonitorOptions options,
        IAzureDevOpsService azdo,
        IHelixService helix,
        MonitorState state)
    {
        _logger = logger;
        _options = options;
        _azdo = azdo;
        _helix = helix;
        _state = state;

        int uploadParallelism = options.TestResultUploadParallelism;
        _jobs = new ActionQueue<JobUploadRequest>(
            parallelism: Math.Min(4, uploadParallelism),
            ExpandJobAsync);
        _workItems = new ActionQueue<WorkItemUploadRequest>(
            capacity: Math.Max(64, uploadParallelism * 8),
            parallelism: uploadParallelism,
            ProcessWorkItemAsync);
        _finalizers = new ActionQueue<JobUploadSession>(
            capacity: Math.Max(64, uploadParallelism * 2),
            parallelism: Math.Min(4, uploadParallelism),
            FinalizeJobAsync);
    }

    public UploadPipelineSnapshot Snapshot => new(
        _jobs.Snapshot,
        _workItems.Snapshot,
        _finalizers.Snapshot,
        _sessions.Values.Count(static session => session.HasFailed),
        _sessions.Values.Sum(static session => session.UploadedResultCount));

    public bool TryEnqueue(HelixJobInfo job, IReadOnlyCollection<WorkItemSummary> workItems)
    {
        if (Volatile.Read(ref _draining) != 0 || _state.IsHelixJobProcessed(job.JobName))
        {
            return false;
        }

        var session = new JobUploadSession(job, workItems);
        if (!_sessions.TryAdd(job.JobName, session))
        {
            return false;
        }

        if (!_jobs.TryEnqueue(new JobUploadRequest(session)))
        {
            _sessions.TryRemove(job.JobName, out _);
            return false;
        }

        _state.TryQueueHelixJobUpload(job.JobName);
        return true;
    }

    public async Task DrainAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _draining, 1) != 0)
        {
            return;
        }

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        _jobs.Complete();
        await _jobs.DrainAsync().WaitAsync(cancellationToken);

        _workItems.Complete();
        await _workItems.DrainAsync().WaitAsync(cancellationToken);

        _finalizers.Complete();
        await _finalizers.DrainAsync().WaitAsync(cancellationToken);

        UploadPipelineSnapshot snapshot = Snapshot;
        _logger.LogInformation(
            "Test result pipeline drained in {Elapsed}. {JobCount} job(s), {WorkItemCount} work item(s), "
            + "and {ResultCount} result(s) were processed; {FailedJobCount} job upload(s) remain untagged.",
            DateTimeOffset.UtcNow - startedAt,
            snapshot.Jobs.Completed,
            snapshot.WorkItems.Completed,
            snapshot.UploadedResults,
            snapshot.FailedJobs);
    }

    public void Cancel()
    {
        _jobs.Cancel();
        _workItems.Cancel();
        _finalizers.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        Cancel();
        await _jobs.DisposeAsync();
        await _workItems.DisposeAsync();
        await _finalizers.DisposeAsync();
    }

    private async ValueTask ExpandJobAsync(JobUploadRequest request, CancellationToken cancellationToken)
    {
        JobUploadSession session = request.Session;
        _state.MarkHelixJobUploadInProgress(session.Job.JobName);

        if (session.WorkItems.Count == 0)
        {
            await _finalizers.EnqueueAsync(session, cancellationToken);
            return;
        }

        foreach (WorkItemSummary workItem in session.WorkItems)
        {
            await _workItems.EnqueueAsync(new WorkItemUploadRequest(session, workItem.Name), cancellationToken);
        }
    }

    private async ValueTask ProcessWorkItemAsync(
        WorkItemUploadRequest request,
        CancellationToken cancellationToken)
    {
        JobUploadSession session = request.Session;
        try
        {
            WorkItemTestResults downloaded = await ExecuteDownloadWithRetryAsync(
                session.Job,
                request.WorkItemName,
                cancellationToken);
            int testRunId = await session.GetOrCreateTestRunAsync(
                () => _azdo.CreateTestRunAsync(session.Job.TestRunName, cancellationToken));
            TestResultUploadSummary summary =
                await _azdo.UploadTestResultsAsync(testRunId, downloaded, cancellationToken);

            session.RecordSuccess(request.WorkItemName, summary);
            if (_options.FailWorkItemsWithFailedTests)
            {
                _state.ObserveTestResult(session.Job.JobName, request.WorkItemName, summary);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            session.RecordFailure();
            LogUploadFailure(
                ex,
                $"process test results for work item '{request.WorkItemName}' in job '{session.Job.DisplayName}'");
        }
        finally
        {
            if (session.MarkWorkItemFinished())
            {
                await _finalizers.EnqueueAsync(session, cancellationToken);
            }
        }
    }

    private async ValueTask FinalizeJobAsync(
        JobUploadSession session,
        CancellationToken cancellationToken)
    {
        if (session.HasFailed)
        {
            _state.MarkHelixJobUploadFailed(session.Job.JobName);
            return;
        }

        try
        {
            int testRunId = await session.GetOrCreateTestRunAsync(
                () => _azdo.CreateTestRunAsync(session.Job.TestRunName, cancellationToken));
            await _azdo.CompleteTestRunAsync(
                testRunId,
                session.Job.JobName,
                session.FailedWorkItems,
                cancellationToken);

            _state.TryMarkHelixJobProcessed(session.Job.JobName);
            _logger.LogInformation(
                "{UploadedCount} test results for job '{JobName}' processed.",
                session.UploadedResultCount,
                session.Job.DisplayName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            session.RecordFailure();
            _state.MarkHelixJobUploadFailed(session.Job.JobName);
            LogUploadFailure(ex, $"complete Azure DevOps test run for job '{session.Job.DisplayName}'");
        }
    }

    private async Task<WorkItemTestResults> ExecuteDownloadWithRetryAsync(
        HelixJobInfo job,
        string workItemName,
        CancellationToken cancellationToken)
    {
        WorkItemTestResults result = null;
        Exception lastException = null;
        var retry = new ExponentialRetry
        {
            MaxAttempts = MaximumTransientDownloadRetries + 1,
            RetryDelayCallback = (attempt, delay) =>
                _logger.LogDebug(
                    "Transient result download failure for '{JobName}/{WorkItemName}' on attempt {Attempt}. "
                    + "Retrying after {Delay}.",
                    job.DisplayName,
                    workItemName,
                    attempt,
                    delay),
        };

        bool succeeded = await retry.RunAsync(
            async _ =>
            {
                try
                {
                    result = await _helix.DownloadTestResultsAsync(
                        job.JobName,
                        workItemName,
                        _options.WorkingDirectory,
                        cancellationToken);
                    return RetryResult.Success;
                }
                catch (Exception ex) when (
                    !cancellationToken.IsCancellationRequested
                    && TransientFailureDetector.IsTransient(ex))
                {
                    lastException = ex;
                    return RetryResult.Retry();
                }
            },
            cancellationToken);

        return succeeded
            ? result
            : throw lastException ?? new InvalidOperationException("Result download retry exited unexpectedly.");
    }

    private void LogUploadFailure(Exception exception, string operation)
        => _logger.LogWarning(
            exception,
            "{Prefix}Failed to {Operation}. The Helix job remains untagged so a later monitor invocation can replay it.",
            AzdoWarningPrefix,
            operation);

    private sealed record JobUploadRequest(JobUploadSession Session);

    private sealed record WorkItemUploadRequest(JobUploadSession Session, string WorkItemName);

    private sealed class JobUploadSession
    {
        private readonly object _sync = new();
        private readonly HashSet<string> _failedWorkItems = new(StringComparer.OrdinalIgnoreCase);
        private Task<int> _testRunTask;
        private int _finishedWorkItems;
        private int _failed;
        private long _uploadedResultCount;

        public JobUploadSession(HelixJobInfo job, IReadOnlyCollection<WorkItemSummary> workItems)
        {
            Job = job;
            WorkItems = [.. workItems];
        }

        public HelixJobInfo Job { get; }

        public IReadOnlyList<WorkItemSummary> WorkItems { get; }

        public bool HasFailed => Volatile.Read(ref _failed) != 0;

        public long UploadedResultCount => Interlocked.Read(ref _uploadedResultCount);

        public IReadOnlyCollection<string> FailedWorkItems
        {
            get
            {
                lock (_sync)
                {
                    return [.. _failedWorkItems];
                }
            }
        }

        public Task<int> GetOrCreateTestRunAsync(Func<Task<int>> create)
        {
            lock (_sync)
            {
                return _testRunTask ??= create();
            }
        }

        public void RecordSuccess(string workItemName, TestResultUploadSummary summary)
        {
            Interlocked.Add(ref _uploadedResultCount, summary.UploadedCount);
            if (!summary.AllPassed)
            {
                lock (_sync)
                {
                    _failedWorkItems.Add(workItemName);
                }
            }
        }

        public void RecordFailure() => Interlocked.Exchange(ref _failed, 1);

        public bool MarkWorkItemFinished()
            => Interlocked.Increment(ref _finishedWorkItems) == WorkItems.Count;
    }
}

internal readonly record struct UploadPipelineSnapshot(
    QueueSnapshot Jobs,
    QueueSnapshot WorkItems,
    QueueSnapshot Finalizers,
    int FailedJobs,
    long UploadedResults);
