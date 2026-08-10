// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Arcade.Common;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.Helix.JobMonitor.Models;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    /// <summary>
    /// Bounded cross-job pipeline for downloading, preparing, and publishing Helix test results.
    /// Work items are scheduled round-robin across active jobs, prepared by a fixed worker pool,
    /// and handed through a bounded queue to an independent publishing pool.
    /// </summary>
    internal sealed class TestResultUploadQueue : IDisposable
    {
        private const int MaximumTransientRetries = 2;
        private const int ActiveJobWindowCapacity = 64;
        private const string AzdoWarningPrefix = "##vso[task.logissue type=warning]";

        private readonly ILogger _logger;
        private readonly JobMonitorOptions _options;
        private readonly IAzureDevOpsService _azdo;
        private readonly IHelixService _helix;
        private readonly MonitorState _monitorState;
        private readonly Channel<JobUpload> _jobs;
        private readonly BoundedRotatingJobQueue _readyJobs;
        private readonly Channel<WorkItemUpload> _preparations;
        private readonly Channel<PreparedUpload> _publications;
        private readonly SemaphoreSlim _processingLimiter;
        private readonly SemaphoreSlim _jobAdmissionPermits;
        private readonly int _jobAdmissionCapacity;
        private readonly Task _dispatchStart;
        private readonly CancellationTokenSource _shutdown = new();
        private readonly Task _dispatcher;
        private readonly Task[] _initializationWorkers;
        private readonly Task[] _preparationWorkers;
        private readonly Task[] _publicationWorkers;
        private readonly object _pendingLock = new();
        private readonly List<JobUpload> _pending = [];
        private readonly object _enqueueLock = new();
        private TaskCompletionSource _enqueueBarrier;
        private int _inProgressEnqueueCount;
        private int _admittedJobCount;
        private int _maximumAdmittedJobCount;
        private bool _acceptingEnqueues = true;
        private int _activePreparations;
        private int _queuedPublications;
        private int _activePublications;
        private bool _disposed;

        public TestResultUploadQueue(
            ILogger logger,
            JobMonitorOptions options,
            IAzureDevOpsService azdo,
            IHelixService helix,
            MonitorState monitorState,
            Task dispatchStart = null)
        {
            _logger = logger;
            _options = options;
            _azdo = azdo;
            _helix = helix;
            _monitorState = monitorState;
            _dispatchStart = dispatchStart ?? Task.CompletedTask;

            int jobCapacity = Math.Max(16, options.TestResultProcessingParallelism * 4);
            int preparationCapacity = options.TestResultProcessingParallelism;
            int publicationCapacity = Math.Max(
                options.TestResultProcessingParallelism,
                options.TestResultUploadParallelism);
            _jobAdmissionCapacity = ActiveJobWindowCapacity + jobCapacity;
            _jobAdmissionPermits = new SemaphoreSlim(
                _jobAdmissionCapacity,
                _jobAdmissionCapacity);

            _jobs = Channel.CreateBounded<JobUpload>(new BoundedChannelOptions(jobCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = options.TestResultProcessingParallelism == 1,
                SingleWriter = false,
            });
            // Retain one bounded waiting tier behind the active window. A completed scheduling
            // turn atomically rotates a partial active job behind that tier, so later jobs get
            // service without admitting an unbounded number of JobUpload states.
            _readyJobs = new BoundedRotatingJobQueue(
                ActiveJobWindowCapacity,
                _shutdown.Token);
            _preparations = Channel.CreateBounded<WorkItemUpload>(new BoundedChannelOptions(preparationCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = options.TestResultProcessingParallelism == 1,
                SingleWriter = true,
            });
            _publications = Channel.CreateBounded<PreparedUpload>(new BoundedChannelOptions(publicationCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = options.TestResultUploadParallelism == 1,
                SingleWriter = false,
            });

            _processingLimiter = new SemaphoreSlim(
                options.TestResultProcessingParallelism,
                options.TestResultProcessingParallelism);
            _dispatcher = DispatchAsync();
            _initializationWorkers =
            [
                ..Enumerable.Range(0, options.TestResultProcessingParallelism)
                    .Select(_ => InitializeJobsAsync())
            ];
            _preparationWorkers =
            [
                ..Enumerable.Range(0, options.TestResultProcessingParallelism)
                    .Select(_ => PrepareAsync())
            ];
            _publicationWorkers =
            [
                ..Enumerable.Range(0, options.TestResultUploadParallelism)
                    .Select(_ => PublishAsync())
            ];
        }

        public async Task EnqueueAsync(
            HelixJobInfo helixJob,
            IReadOnlyCollection<WorkItemSummary> workItems,
            CancellationToken cancellationToken)
        {
            BeginEnqueue();
            JobAdmissionLease admission = null;
            JobUpload upload = null;
            try
            {
                admission = await AcquireJobAdmissionAsync(cancellationToken);

                string[] workItemNames =
                [
                    ..workItems
                        .Select(w => w.Name)
                        .Where(name => !string.IsNullOrEmpty(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                ];
                upload = new JobUpload(
                    helixJob,
                    workItemNames,
                    cancellationToken,
                    admission);
                admission = null;

                lock (_pendingLock)
                {
                    _pending.Add(upload);
                }

                _logger.LogInformation(
                    "Queued {Count} work item(s) from job '{JobName}' for test-result processing.",
                    workItemNames.Length,
                    helixJob.DisplayName);
                LogProgress(upload, "queued");

                await _jobs.Writer.WriteAsync(upload, cancellationToken);
            }
            catch
            {
                if (upload is not null)
                {
                    upload.ReleaseAdmission();
                    FinalizeFailedJob(upload);
                }
                else
                {
                    admission?.Release();
                }
                throw;
            }
            finally
            {
                EndEnqueue();
            }
        }

        public void Prune()
        {
            lock (_pendingLock)
            {
                _pending.RemoveAll(static upload => upload.Completion.IsCompleted);
            }
        }

        internal (int ActivePreparations, int QueuedPublications, int ActivePublications) SnapshotOccupancy()
            => (
                Volatile.Read(ref _activePreparations),
                Volatile.Read(ref _queuedPublications),
                Volatile.Read(ref _activePublications));

        internal (
            int Admitted,
            int MaximumAdmitted,
            int Capacity,
            int AvailablePermits,
            int ScheduledRetained,
            int Active,
            int ActiveCapacity,
            int Waiting) SnapshotJobAdmission()
        {
            (int retained, int active, int activeCapacity, int waiting) = _readyJobs.Snapshot();
            return (
                Volatile.Read(ref _admittedJobCount),
                Volatile.Read(ref _maximumAdmittedJobCount),
                _jobAdmissionCapacity,
                _jobAdmissionPermits.CurrentCount,
                retained,
                active,
                activeCapacity,
                waiting);
        }

        private void BeginEnqueue()
        {
            lock (_enqueueLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (!_acceptingEnqueues)
                {
                    throw new InvalidOperationException(
                        "Test-result processing is draining and no longer accepts new jobs.");
                }

                _inProgressEnqueueCount++;
            }
        }

        private void EndEnqueue()
        {
            lock (_enqueueLock)
            {
                _inProgressEnqueueCount--;
                if (_inProgressEnqueueCount == 0)
                {
                    _enqueueBarrier?.TrySetResult();
                }
            }
        }

        private Task BeginDrain()
        {
            lock (_enqueueLock)
            {
                _acceptingEnqueues = false;
                if (_inProgressEnqueueCount == 0)
                {
                    return Task.CompletedTask;
                }

                _enqueueBarrier ??= new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                return _enqueueBarrier.Task;
            }
        }

        private async Task<JobAdmissionLease> AcquireJobAdmissionAsync(
            CancellationToken cancellationToken)
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _shutdown.Token);
            bool acquired = false;
            try
            {
                await _jobAdmissionPermits.WaitAsync(linkedCancellation.Token);
                acquired = true;
                linkedCancellation.Token.ThrowIfCancellationRequested();

                int admitted = Interlocked.Increment(ref _admittedJobCount);
                int observed;
                while (admitted > (observed = Volatile.Read(ref _maximumAdmittedJobCount)))
                {
                    if (Interlocked.CompareExchange(
                        ref _maximumAdmittedJobCount,
                        admitted,
                        observed) == observed)
                    {
                        break;
                    }
                }

                acquired = false;
                return new JobAdmissionLease(this);
            }
            finally
            {
                if (acquired)
                {
                    _jobAdmissionPermits.Release();
                }
            }
        }

        private void ReleaseJobAdmission()
        {
            Interlocked.Decrement(ref _admittedJobCount);
            _jobAdmissionPermits.Release();
        }

        public async Task DrainAsync(CancellationToken cancellationToken)
        {
            await BeginDrain().WaitAsync(cancellationToken);

            bool loggedWait = false;
            while (true)
            {
                JobUpload[] pending;
                lock (_pendingLock)
                {
                    _pending.RemoveAll(static upload => upload.Completion.IsCompleted);
                    pending = [.._pending];
                }

                if (pending.Length == 0)
                {
                    return;
                }

                if (!loggedWait)
                {
                    _logger.LogInformation(
                        "Waiting for {Count} pending test result upload(s) to complete.",
                        pending.Length);
                    loggedWait = true;
                }

                Task allUploads = Task.WhenAll(pending.Select(upload => upload.Completion));
                if (_options.Verbose)
                {
                    LogPendingUploads(pending);
                    TimeSpan heartbeatInterval = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));
                    while (!allUploads.IsCompleted)
                    {
                        Task heartbeat = Task.Delay(heartbeatInterval, cancellationToken);
                        if (await Task.WhenAny(allUploads, heartbeat) == allUploads)
                        {
                            break;
                        }

                        cancellationToken.ThrowIfCancellationRequested();
                        LogPendingUploads(pending.Where(upload => !upload.Completion.IsCompleted));
                    }
                }

                await allUploads.WaitAsync(cancellationToken);
            }
        }

        private async Task InitializeJobsAsync()
        {
            try
            {
                await foreach (JobUpload job in _jobs.Reader.ReadAllAsync(_shutdown.Token))
                {
                    await InitializeJobAsync(job);
                }
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
            }
        }

        private async Task InitializeJobAsync(JobUpload job)
        {
            bool initialized = false;
            bool limiterAcquired = false;
            try
            {
                await _processingLimiter.WaitAsync(_shutdown.Token);
                limiterAcquired = true;
                Interlocked.Increment(ref _activePreparations);
                _monitorState.MarkHelixJobUploadInProgress(job.HelixJob.JobName);
                LogProgress(job, "resolving Helix results context");
                job.CancellationToken.ThrowIfCancellationRequested();

                OperationResult<HelixTestResultsContext> context = await TryExecuteWithRetryAsync(
                    () => _helix.CreateTestResultsContextAsync(
                        job.HelixJob.JobName,
                        _options.WorkingDirectory,
                        job.CancellationToken),
                    "resolve the Helix results context",
                    job.HelixJob,
                    workItemName: null,
                    testRunId: 0,
                    retryCount: MaximumTransientRetries,
                    job.CancellationToken);
                if (context.Success)
                {
                    job.SetResultsContext(context.Result);
                    initialized = true;
                }
            }
            catch (OperationCanceledException) when (
                job.CancellationToken.IsCancellationRequested || _shutdown.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "{Prefix}Unexpected failure resolving the Helix results context for job '{JobName}'. "
                    + "The run remains untagged and a later monitor invocation may retry the upload.",
                    AzdoWarningPrefix,
                    job.HelixJob.DisplayName);
            }
            finally
            {
                if (limiterAcquired)
                {
                    Interlocked.Decrement(ref _activePreparations);
                    _processingLimiter.Release();
                }
            }

            if (!initialized)
            {
                job.ReleaseAdmission();
                ExecuteTerminalAction(job, job.FailAllWorkItems());
                return;
            }

            try
            {
                LogProgress(job, "queued for work-item preparation");
                _readyJobs.Enqueue(job);
            }
            catch (ObjectDisposedException) when (_shutdown.IsCancellationRequested)
            {
                job.ReleaseAdmission();
                ExecuteTerminalAction(job, job.FailAllWorkItems());
            }
        }

        private async Task DispatchAsync()
        {
            try
            {
                await _dispatchStart.WaitAsync(_shutdown.Token);
                while (true)
                {
                    JobUpload job = await _readyJobs.TakeAsync();
                    bool hasMoreWork = false;
                    try
                    {
                        if (job.CancellationToken.IsCancellationRequested)
                        {
                            ExecuteTerminalAction(job, job.SkipUnscheduledWorkItems());
                            continue;
                        }

                        if (job.TryTakeNextWorkItem(out WorkItemUpload workItem))
                        {
                            await _preparations.Writer.WriteAsync(workItem, _shutdown.Token);
                        }

                        hasMoreWork = job.HasUnscheduledWorkItems;
                    }
                    finally
                    {
                        _readyJobs.CompleteTurn(job, hasMoreWork);
                        if (!hasMoreWork)
                        {
                            job.ReleaseAdmission();
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (_shutdown.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Test-result pipeline dispatcher failed.");
                FailAllPendingJobs();
            }
        }

        private async Task PrepareAsync()
        {
            try
            {
                await foreach (WorkItemUpload workItem in _preparations.Reader.ReadAllAsync(_shutdown.Token))
                {
                    await PrepareWorkItemAsync(workItem);
                }
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
            }
        }

        private async Task PrepareWorkItemAsync(WorkItemUpload workItem)
        {
            JobUpload job = workItem.Job;
            bool handedOff = false;
            bool preparedForPublication = false;
            bool limiterAcquired = false;
            bool preparationBegan = false;
            PreparedUpload publication = null;

            try
            {
                await _processingLimiter.WaitAsync(_shutdown.Token);
                limiterAcquired = true;
                job.BeginPreparation();
                preparationBegan = true;
                Interlocked.Increment(ref _activePreparations);
                LogProgress(job, workItem.CompletionOnly ? "preparing job completion" : $"preparing '{workItem.WorkItemName}'");
                job.CancellationToken.ThrowIfCancellationRequested();

                PreparedWorkItemTestResults prepared = null;
                if (!workItem.CompletionOnly)
                {
                    OperationResult<WorkItemTestResults> downloaded = await TryExecuteWithRetryAsync(
                        () => _helix.DownloadTestResultsAsync(
                            job.ResultsContext,
                            workItem.WorkItemName,
                            job.CancellationToken),
                        "download the Helix test results",
                        job.HelixJob,
                        workItem.WorkItemName,
                        testRunId: 0,
                        retryCount: MaximumTransientRetries,
                        job.CancellationToken);
                    if (!downloaded.Success)
                    {
                        return;
                    }

                    OperationResult<PreparedWorkItemTestResults> preparation = await TryExecuteWithRetryAsync(
                        () => _azdo.PrepareTestResultsAsync(downloaded.Result, job.CancellationToken),
                        "prepare the test results",
                        job.HelixJob,
                        workItem.WorkItemName,
                        testRunId: 0,
                        retryCount: MaximumTransientRetries,
                        job.CancellationToken);
                    if (!preparation.Success)
                    {
                        return;
                    }

                    prepared = preparation.Result;
                }

                LogProgress(job, workItem.CompletionOnly
                    ? "waiting to publish job completion"
                    : $"prepared '{workItem.WorkItemName}', waiting to publish");
                job.MovePreparationToPublicationQueue();
                preparedForPublication = true;
                Interlocked.Increment(ref _queuedPublications);
                publication = new PreparedUpload(
                    job,
                    workItem.WorkItemName,
                    workItem.CompletionOnly,
                    prepared);
                await _publications.Writer.WriteAsync(publication, _shutdown.Token);
                handedOff = true;
            }
            catch (OperationCanceledException) when (
                job.CancellationToken.IsCancellationRequested || _shutdown.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "{Prefix}Unexpected failure preparing test results for '{JobName}/{WorkItemName}'. "
                    + "The run remains untagged and a later monitor invocation may retry the upload.",
                    AzdoWarningPrefix,
                    job.HelixJob.DisplayName,
                    workItem.WorkItemName);
            }
            finally
            {
                if (limiterAcquired)
                {
                    Interlocked.Decrement(ref _activePreparations);
                    _processingLimiter.Release();
                }
                publication?.ReleasePreparationSlot();
                if (!handedOff && preparationBegan)
                {
                    if (preparedForPublication)
                    {
                        Interlocked.Decrement(ref _queuedPublications);
                        ExecuteTerminalAction(job, job.CompleteQueuedPublication(success: false));
                    }
                    else
                    {
                        ExecuteTerminalAction(job, job.CompletePreparation(success: false));
                    }
                }
            }
        }

        private async Task PublishAsync()
        {
            try
            {
                await foreach (PreparedUpload prepared in _publications.Reader.ReadAllAsync(_shutdown.Token))
                {
                    await PublishWorkItemAsync(prepared);
                }
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
            }
        }

        private async Task PublishWorkItemAsync(PreparedUpload prepared)
        {
            JobUpload job = prepared.Job;
            bool terminalRecorded = false;
            await prepared.PreparationSlotReleased;
            Interlocked.Decrement(ref _queuedPublications);
            job.BeginPublication();
            Interlocked.Increment(ref _activePublications);
            LogProgress(job, prepared.CompletionOnly ? "publishing job completion" : $"publishing '{prepared.WorkItemName}'");

            try
            {
                job.CancellationToken.ThrowIfCancellationRequested();

                OperationResult<int> testRun = await job.GetOrCreateTestRunAsync(() => TryExecuteAsync(
                    () => _azdo.CreateTestRunAsync(job.HelixJob.TestRunName, job.CancellationToken),
                    "create the Azure DevOps test run",
                    job.HelixJob,
                    workItemName: null,
                    testRunId: 0,
                    job.CancellationToken));
                if (!testRun.Success)
                {
                    terminalRecorded = true;
                    ExecuteTerminalAction(job, job.CompletePublication(success: false, prepared.WorkItemName, summary: null));
                    return;
                }

                TestResultUploadSummary summary = new(true, 0);
                if (!prepared.CompletionOnly)
                {
                    OperationResult<TestResultUploadSummary> publication = await TryExecuteAsync(
                        () => _azdo.PublishTestResultsAsync(
                            testRun.Result,
                            prepared.Results,
                            job.CancellationToken),
                        "publish the test results to Azure DevOps",
                        job.HelixJob,
                        prepared.WorkItemName,
                        testRun.Result,
                        job.CancellationToken);
                    if (!publication.Success)
                    {
                        terminalRecorded = true;
                        ExecuteTerminalAction(job, job.CompletePublication(success: false, prepared.WorkItemName, summary: null));
                        return;
                    }

                    summary = publication.Result;
                }

                terminalRecorded = true;
                TerminalAction action = job.CompletePublication(
                    success: true,
                    prepared.WorkItemName,
                    summary);
                await ExecuteTerminalActionAsync(job, action, testRun.Result);
            }
            catch (OperationCanceledException) when (
                job.CancellationToken.IsCancellationRequested || _shutdown.IsCancellationRequested)
            {
                if (!terminalRecorded)
                {
                    ExecuteTerminalAction(job, job.CompletePublication(success: false, prepared.WorkItemName, summary: null));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "{Prefix}Unexpected failure publishing test results for '{JobName}/{WorkItemName}'. "
                    + "The run remains untagged and a later monitor invocation may retry the upload.",
                    AzdoWarningPrefix,
                    job.HelixJob.DisplayName,
                    prepared.WorkItemName);
                if (!terminalRecorded)
                {
                    ExecuteTerminalAction(job, job.CompletePublication(success: false, prepared.WorkItemName, summary: null));
                }
                else
                {
                    FinalizeFailedJob(job);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _activePublications);
            }
        }

        private async Task ExecuteTerminalActionAsync(JobUpload job, TerminalAction action, int testRunId)
        {
            if (action == TerminalAction.None)
            {
                return;
            }

            if (action == TerminalAction.Fail)
            {
                FinalizeFailedJob(job);
                return;
            }

            try
            {
                IReadOnlyCollection<string> failedWorkItems = job.SnapshotFailedWorkItems();
                if (_options.FailWorkItemsWithFailedTests && failedWorkItems.Count > 0)
                {
                    _monitorState.ObserveTestResults(failedWorkItems.ToDictionary(
                        workItemName => (job.HelixJob.JobName, workItemName),
                        _ => new TestResultUploadSummary(false, 0)));
                }

                OperationResult<bool> completed = await TryExecuteAsync(
                    async () =>
                    {
                        await _azdo.CompleteTestRunAsync(
                            testRunId,
                            job.HelixJob.JobName,
                            failedWorkItems,
                            job.CancellationToken);
                        return true;
                    },
                    "complete and tag the Azure DevOps test run",
                    job.HelixJob,
                    workItemName: null,
                    testRunId,
                    job.CancellationToken);
                if (!completed.Success)
                {
                    FinalizeFailedJob(job);
                    return;
                }

                _monitorState.TryMarkHelixJobProcessed(job.HelixJob.JobName);
                if (job.TryFinish())
                {
                    LogProgress(job, "completed");
                    _logger.LogInformation(
                        "{UploadedCount} test results for job '{JobName}' processed.",
                        job.UploadedCount,
                        job.HelixJob.DisplayName);
                }
            }
            catch (OperationCanceledException) when (
                job.CancellationToken.IsCancellationRequested || _shutdown.IsCancellationRequested)
            {
                FinalizeFailedJob(job);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "{Prefix}Unexpected failure completing test-result processing for job '{JobName}'. "
                    + "The run remains untagged and a later monitor invocation may retry the upload.",
                    AzdoWarningPrefix,
                    job.HelixJob.DisplayName);
                FinalizeFailedJob(job);
            }
        }

        private void ExecuteTerminalAction(JobUpload job, TerminalAction action)
        {
            if (action == TerminalAction.Fail)
            {
                FinalizeFailedJob(job);
            }
        }

        private void FinalizeFailedJob(JobUpload job)
        {
            _monitorState.MarkHelixJobUploadFailed(job.HelixJob.JobName);
            if (job.TryFinish())
            {
                LogProgress(job, "failed");
            }
        }

        private Task<OperationResult<T>> TryExecuteAsync<T>(
            Func<Task<T>> operation,
            string operationDescription,
            HelixJobInfo helixJob,
            string workItemName,
            int testRunId,
            CancellationToken cancellationToken)
            => TryExecuteWithRetryAsync(
                operation,
                operationDescription,
                helixJob,
                workItemName,
                testRunId,
                retryCount: 0,
                cancellationToken);

        private async Task<OperationResult<T>> TryExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            string operationDescription,
            HelixJobInfo helixJob,
            string workItemName,
            int testRunId,
            int retryCount,
            CancellationToken cancellationToken)
        {
            try
            {
                T result = default;
                Exception lastException = null;
                var retry = new ExponentialRetry
                {
                    MaxAttempts = retryCount + 1,
                    RetryDelayCallback = (failedAttempt, delay) =>
                        _logger.LogDebug(
                            "Failed to {OperationDescription} for '{JobName}/{WorkItemName}'. Test run ID was {TestRunId}. "
                            + "Waiting {RetryDelay} before attempt {NextAttempt} of {AttemptCount}.",
                            operationDescription,
                            helixJob.DisplayName,
                            workItemName ?? "(job)",
                            testRunId,
                            delay,
                            failedAttempt + 1,
                            retryCount + 1),
                };

                bool succeeded = await retry.RunAsync(
                    async attempt =>
                    {
                        try
                        {
                            result = await operation();
                            return true;
                        }
                        catch (Exception ex) when (
                            !cancellationToken.IsCancellationRequested
                            && TransientFailureDetector.IsTransient(ex))
                        {
                            lastException = ex;
                            _logger.LogDebug(
                                ex,
                                "Failed to {OperationDescription} for '{JobName}/{WorkItemName}'. Test run ID was {TestRunId}. "
                                + "Transient attempt {Attempt} of {AttemptCount} failed.",
                                operationDescription,
                                helixJob.DisplayName,
                                workItemName ?? "(job)",
                                testRunId,
                                attempt + 1,
                                retryCount + 1);
                            return false;
                        }
                    },
                    cancellationToken);

                if (!succeeded)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw lastException ?? new InvalidOperationException("Upload retry loop exited unexpectedly.");
                }

                return new OperationResult<T>(true, result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                string failureKind = TransientFailureDetector.IsTransient(ex)
                    ? retryCount > 0
                        ? "Transient retry limit reached."
                        : "The operation may have partially completed and is not safe to replay in this invocation."
                    : "The failure is not retryable.";
                _logger.LogWarning(
                    ex,
                    "{Prefix}Failed to {OperationDescription} for '{JobName}/{WorkItemName}'. Test run ID was {TestRunId}. "
                    + "{FailureKind} The run remains untagged and a later monitor invocation may retry the upload.",
                    AzdoWarningPrefix,
                    operationDescription,
                    helixJob.DisplayName,
                    workItemName ?? "(job)",
                    testRunId,
                    failureKind);
                return new OperationResult<T>(false, default);
            }
        }

        private void LogProgress(JobUpload job, string phase)
        {
            job.SetPhase(phase);
            if (!_options.Verbose)
            {
                return;
            }

            JobProgress progress = job.SnapshotProgress();
            _logger.LogDebug(
                "Test result pipeline: job '{JobName}', phase='{Phase}', "
                + "queued={Queued}, preparing={Preparing}, prepared={Prepared}, publishing={Publishing}, completed={Completed}/{Total}; "
                + "processing limiter={ActivePreparations}/{ProcessingLimit}, publishing limiter={ActivePublications}/{PublicationLimit}, "
                + "prepared queue={PreparedQueue}.",
                job.HelixJob.DisplayName,
                phase,
                progress.Queued,
                progress.Preparing,
                progress.Prepared,
                progress.Publishing,
                progress.Completed,
                progress.Total,
                Volatile.Read(ref _activePreparations),
                _options.TestResultProcessingParallelism,
                Volatile.Read(ref _activePublications),
                _options.TestResultUploadParallelism,
                Volatile.Read(ref _queuedPublications));
        }

        private void LogPendingUploads(IEnumerable<JobUpload> uploads)
        {
            JobUpload[] pending = [..uploads];
            if (pending.Length == 0)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            string details = string.Join(
                Environment.NewLine,
                pending
                    .OrderBy(upload => upload.StartedAt)
                    .Select(upload =>
                    {
                        JobProgress progress = upload.SnapshotProgress();
                        return $"- {upload.HelixJob.DisplayName}: phase='{progress.Phase}', "
                            + $"queued={progress.Queued}, preparing={progress.Preparing}, prepared={progress.Prepared}, "
                            + $"publishing={progress.Publishing}, completed={progress.Completed}/{progress.Total}, "
                            + $"phase elapsed={now - progress.PhaseStartedAt:c}, "
                            + $"total elapsed={now - upload.StartedAt:c}";
                    }));

            _logger.LogDebug(
                "{Count} test result upload(s) remain pending "
                + "(processing limiter={ActivePreparations}/{ProcessingLimit}, "
                + "publishing limiter={ActivePublications}/{PublicationLimit}, prepared queue={PreparedQueue}):{nl}{Details}",
                pending.Length,
                Volatile.Read(ref _activePreparations),
                _options.TestResultProcessingParallelism,
                Volatile.Read(ref _activePublications),
                _options.TestResultUploadParallelism,
                Volatile.Read(ref _queuedPublications),
                Environment.NewLine,
                details);
        }

        private void FailAllPendingJobs()
        {
            JobUpload[] pending;
            lock (_pendingLock)
            {
                pending = [.._pending];
            }

            foreach (JobUpload job in pending)
            {
                job.ReleaseAdmission();
                FinalizeFailedJob(job);
            }
        }

        public void Dispose()
        {
            lock (_enqueueLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _acceptingEnqueues = false;
            }

            _shutdown.Cancel();
            _jobs.Writer.TryComplete();
            _readyJobs.Dispose();
            _preparations.Writer.TryComplete();
            _publications.Writer.TryComplete();
            FailAllPendingJobs();
            ObserveTask(_dispatcher);
            foreach (Task worker in _initializationWorkers)
            {
                ObserveTask(worker);
            }
            foreach (Task worker in _preparationWorkers)
            {
                ObserveTask(worker);
            }
            foreach (Task worker in _publicationWorkers)
            {
                ObserveTask(worker);
            }
        }

        private static void ObserveTask(Task task)
        {
            if (task.IsFaulted)
            {
                _ = task.Exception;
            }
        }

        private sealed class JobAdmissionLease(TestResultUploadQueue owner)
        {
            private int _released;

            public void Release()
            {
                if (Interlocked.Exchange(ref _released, 1) == 0)
                {
                    owner.ReleaseJobAdmission();
                }
            }
        }

        private sealed class BoundedRotatingJobQueue : IDisposable
        {
            private readonly object _sync = new();
            private readonly Queue<JobUpload> _active = [];
            private readonly Queue<JobUpload> _waiting = [];
            private readonly SemaphoreSlim _activeAvailable = new(0);
            private readonly int _activeCapacity;
            private readonly CancellationToken _shutdownToken;
            private int _inFlightCount;
            private int _retainedCount;
            private bool _disposed;

            public BoundedRotatingJobQueue(
                int activeCapacity,
                CancellationToken shutdownToken)
            {
                if (activeCapacity <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(activeCapacity));
                }

                _activeCapacity = activeCapacity;
                _shutdownToken = shutdownToken;
            }

            public void Enqueue(JobUpload job)
            {
                lock (_sync)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    _waiting.Enqueue(job);
                    _retainedCount++;
                    PromoteWaitingJobsLocked();
                }
            }

            public async Task<JobUpload> TakeAsync()
            {
                await _activeAvailable.WaitAsync(_shutdownToken);
                lock (_sync)
                {
                    ObjectDisposedException.ThrowIf(_disposed, this);
                    JobUpload job = _active.Dequeue();
                    _inFlightCount++;
                    return job;
                }
            }

            public void CompleteTurn(JobUpload job, bool hasMoreWork)
            {
                lock (_sync)
                {
                    if (_inFlightCount <= 0)
                    {
                        throw new InvalidOperationException("No admitted job turn is in flight.");
                    }

                    _inFlightCount--;
                    if (hasMoreWork && !_disposed)
                    {
                        // New and previously rotated jobs stay ahead of the just-serviced job.
                        // Promotion below atomically swaps the serviced job out of the active
                        // window when anything is waiting.
                        _waiting.Enqueue(job);
                    }
                    else
                    {
                        _retainedCount--;
                    }

                    PromoteWaitingJobsLocked();
                }

            }

            public (
                int Retained,
                int Active,
                int ActiveCapacity,
                int Waiting) Snapshot()
            {
                lock (_sync)
                {
                    return (
                        _retainedCount,
                        _active.Count + _inFlightCount,
                        _activeCapacity,
                        _waiting.Count);
                }
            }

            private void PromoteWaitingJobsLocked()
            {
                if (_disposed)
                {
                    return;
                }

                while (_active.Count + _inFlightCount < _activeCapacity
                    && _waiting.TryDequeue(out JobUpload job))
                {
                    _active.Enqueue(job);
                    _activeAvailable.Release();
                }
            }

            public void Dispose()
            {
                lock (_sync)
                {
                    _disposed = true;
                }
            }
        }

        private readonly record struct OperationResult<T>(bool Success, T Result);

        private enum TerminalAction
        {
            None,
            Fail,
            Complete,
        }

        private sealed record WorkItemUpload(
            JobUpload Job,
            string WorkItemName,
            bool CompletionOnly);

        private sealed class PreparedUpload(
            JobUpload job,
            string workItemName,
            bool completionOnly,
            PreparedWorkItemTestResults results)
        {
            private readonly TaskCompletionSource _preparationSlotReleased =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public JobUpload Job { get; } = job;

            public string WorkItemName { get; } = workItemName;

            public bool CompletionOnly { get; } = completionOnly;

            public PreparedWorkItemTestResults Results { get; } = results;

            public Task PreparationSlotReleased => _preparationSlotReleased.Task;

            public void ReleasePreparationSlot()
                => _preparationSlotReleased.TrySetResult();
        }

        private sealed record JobProgress(
            string Phase,
            int Queued,
            int Preparing,
            int Prepared,
            int Publishing,
            int Completed,
            int Total,
            DateTimeOffset PhaseStartedAt);

        private sealed class JobUpload
        {
            private readonly object _sync = new();
            private readonly string[] _workItemNames;
            private readonly HashSet<string> _failedWorkItems = new(StringComparer.OrdinalIgnoreCase);
            private readonly TaskCompletionSource _completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _nextWorkItem;
            private int _queued;
            private int _preparing;
            private int _prepared;
            private int _publishing;
            private int _completed;
            private long _uploadedCount;
            private bool _failed;
            private bool _finished;
            private string _phase = "queued";
            private DateTimeOffset _phaseStartedAt;
            private HelixTestResultsContext _resultsContext;
            private Task<OperationResult<int>> _testRun;
            private readonly JobAdmissionLease _admission;

            public JobUpload(
                HelixJobInfo helixJob,
                string[] workItemNames,
                CancellationToken cancellationToken,
                JobAdmissionLease admission)
            {
                HelixJob = helixJob;
                _workItemNames = workItemNames;
                CancellationToken = cancellationToken;
                _admission = admission;
                StartedAt = DateTimeOffset.UtcNow;
                _phaseStartedAt = StartedAt;
                _queued = Total;
            }

            public HelixJobInfo HelixJob { get; }

            public CancellationToken CancellationToken { get; }

            public DateTimeOffset StartedAt { get; }

            public Task Completion => _completion.Task;

            public int Total => Math.Max(1, _workItemNames.Length);

            public long UploadedCount
            {
                get { lock (_sync) { return _uploadedCount; } }
            }

            public void ReleaseAdmission()
                => _admission.Release();

            public HelixTestResultsContext ResultsContext
            {
                get
                {
                    lock (_sync)
                    {
                        return _resultsContext
                            ?? throw new InvalidOperationException("The Helix results context has not been initialized.");
                    }
                }
            }

            public bool HasUnscheduledWorkItems
            {
                get { lock (_sync) { return _nextWorkItem < Total; } }
            }

            public bool TryTakeNextWorkItem(out WorkItemUpload workItem)
            {
                lock (_sync)
                {
                    if (_nextWorkItem >= Total)
                    {
                        workItem = null;
                        return false;
                    }

                    bool completionOnly = _workItemNames.Length == 0;
                    string workItemName = completionOnly ? null : _workItemNames[_nextWorkItem];
                    _nextWorkItem++;
                    workItem = new WorkItemUpload(this, workItemName, completionOnly);
                    return true;
                }
            }

            public TerminalAction SkipUnscheduledWorkItems()
            {
                lock (_sync)
                {
                    int skipped = Total - _nextWorkItem;
                    _nextWorkItem = Total;
                    _queued -= skipped;
                    _completed += skipped;
                    _failed = true;
                    return GetTerminalActionLocked();
                }
            }

            public TerminalAction FailAllWorkItems()
            {
                lock (_sync)
                {
                    _nextWorkItem = Total;
                    _queued = 0;
                    _completed = Total;
                    _failed = true;
                    return TerminalAction.Fail;
                }
            }

            public void BeginPreparation()
            {
                lock (_sync)
                {
                    _queued--;
                    _preparing++;
                }
            }

            public void MovePreparationToPublicationQueue()
            {
                lock (_sync)
                {
                    _preparing--;
                    _prepared++;
                }
            }

            public TerminalAction CompletePreparation(bool success)
            {
                lock (_sync)
                {
                    _preparing--;
                    _completed++;
                    _failed |= !success;
                    return GetTerminalActionLocked();
                }
            }

            public TerminalAction CompleteQueuedPublication(bool success)
            {
                lock (_sync)
                {
                    _prepared--;
                    _completed++;
                    _failed |= !success;
                    return GetTerminalActionLocked();
                }
            }

            public void BeginPublication()
            {
                lock (_sync)
                {
                    _prepared--;
                    _publishing++;
                }
            }

            public TerminalAction CompletePublication(
                bool success,
                string workItemName,
                TestResultUploadSummary summary)
            {
                lock (_sync)
                {
                    _publishing--;
                    _completed++;
                    _failed |= !success;
                    if (success && summary is not null)
                    {
                        _uploadedCount += summary.UploadedCount;
                        if (!summary.AllPassed && !string.IsNullOrEmpty(workItemName))
                        {
                            _failedWorkItems.Add(workItemName);
                        }
                    }

                    return GetTerminalActionLocked();
                }
            }

            public void SetResultsContext(HelixTestResultsContext context)
            {
                lock (_sync)
                {
                    _resultsContext = context
                        ?? throw new ArgumentNullException(nameof(context));
                }
            }

            public Task<OperationResult<int>> GetOrCreateTestRunAsync(
                Func<Task<OperationResult<int>>> factory)
            {
                lock (_sync)
                {
                    return _testRun ??= factory();
                }
            }

            public IReadOnlyCollection<string> SnapshotFailedWorkItems()
            {
                lock (_sync)
                {
                    return [.._failedWorkItems.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];
                }
            }

            public JobProgress SnapshotProgress()
            {
                lock (_sync)
                {
                    return new JobProgress(
                        _phase,
                        _queued,
                        _preparing,
                        _prepared,
                        _publishing,
                        _completed,
                        Total,
                        _phaseStartedAt);
                }
            }

            public void SetPhase(string phase)
            {
                lock (_sync)
                {
                    _phase = phase;
                    _phaseStartedAt = DateTimeOffset.UtcNow;
                }
            }

            public bool TryFinish()
            {
                lock (_sync)
                {
                    if (_finished)
                    {
                        return false;
                    }

                    _finished = true;
                    _completion.TrySetResult();
                    return true;
                }
            }

            private TerminalAction GetTerminalActionLocked()
            {
                if (_completed < Total)
                {
                    return TerminalAction.None;
                }

                return _failed ? TerminalAction.Fail : TerminalAction.Complete;
            }
        }
    }
}
