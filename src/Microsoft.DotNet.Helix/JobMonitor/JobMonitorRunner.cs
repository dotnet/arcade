// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client;
using Microsoft.DotNet.Helix.JobMonitor.Models;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    /// <summary>Lifecycle composition root for retry planning, polling, reporting, and uploads.</summary>
    internal sealed class JobMonitorRunner : IJobMonitorRunner, IDisposable
    {
        private readonly JobMonitorOptions _options;
        private readonly ILogger _logger;
        private readonly IAzureDevOpsService _azdo;
        private readonly IHelixService _helix;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly MonitorLedger _ledger = new();
        private readonly StatusReporter _reporter;
        private readonly MonitorPoller _poller;
        private readonly RetryPlanner _retries;
        private readonly TestResultUploadPipeline _uploads;

        public JobMonitorRunner(JobMonitorOptions options, ILogger logger)
            : this(options, logger,
                new AzureDevOpsService(options, logger),
                new HelixService(string.IsNullOrEmpty(options.HelixAccessToken)
                    ? ApiFactory.GetAnonymous(options.HelixBaseUri)
                    : ApiFactory.GetAuthenticated(options.HelixBaseUri, options.HelixAccessToken), logger),
                null)
        {
        }

        internal JobMonitorRunner(JobMonitorOptions options, ILogger logger, IAzureDevOpsService azdo,
            IHelixService helix, Func<TimeSpan, CancellationToken, Task> delayFunc)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _azdo = azdo ?? throw new ArgumentNullException(nameof(azdo));
            _helix = helix ?? throw new ArgumentNullException(nameof(helix));
            _delay = delayFunc ?? Task.Delay;
            Directory.CreateDirectory(options.WorkingDirectory);
            string source = HelixJobSource.Compute(options.BuildReason, options.TeamProject, options.Organization,
                options.RepositoryName, options.SourceBranch);
            _reporter = new StatusReporter(logger, options, _ledger);
            _poller = new MonitorPoller(options, azdo, helix, source);
            _retries = new RetryPlanner(options, azdo, helix, _ledger, _reporter, _poller, source);
            _uploads = new TestResultUploadPipeline(logger, options, azdo, helix, _ledger);
        }

        public async Task<int> RunAsync(CancellationToken cancellationToken)
        {
            _reporter.LogMonitorStart();
            try
            {
                // Reconstruct the only cross-invocation state we trust before making retry or
                // upload decisions. Everything else in the ledger is rebuilt from service data.
                _ledger.AddProcessedHelixJobs(await _azdo.GetProcessedHelixJobNamesAsync(cancellationToken));
                IReadOnlyList<HelixJobInfo> firstPollJobs = await _retries.ExecuteAsync(cancellationToken);
                return await PollAsync(firstPollJobs, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutdown favors releasing Helix capacity over finishing result publication.
                // Untagged uploads are intentionally recoverable by the next monitor invocation.
                _uploads.Abandon();
                _reporter.ReportTimeout();
                using var cancellationBudget = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await CancelLatestInFlightAsync(cancellationBudget.Token);
                return 1;
            }
        }

        private async Task<int> PollAsync(IReadOnlyList<HelixJobInfo> firstPollJobs, CancellationToken cancellationToken)
        {
            var status = new PollStatus();
            while (true)
            {
                // A snapshot is the consistency boundary for one loop iteration: completion,
                // reconciliation, status, and termination all use the same service observations.
                MonitorSnapshot snapshot = await _poller.CaptureAsync(firstPollJobs, cancellationToken);
                firstPollJobs = null;
                _ledger.SetTimelineRecords(snapshot.TimelineRecords);
                _ledger.ObserveJobs(snapshot.StageJobs);

                // Apply old incarnations before replacements so a newer pass can supersede an
                // older failure in the logical work-item outcome map.
                foreach (HelixJobInfo job in MonitorLedger.OrderHelixJobsOldToNew(
                    snapshot.StageJobs.Where(job => snapshot.CompletedJobNames.Contains(job.JobName))))
                {
                    await ReconcileCompletedAsync(job, snapshot.WorkItemsByJob[job.JobName], cancellationToken);
                }

                bool logStatus = _options.Verbose
                    || status.JobCount != snapshot.StageJobs.Count
                    || status.CompletedCount != snapshot.CompletedJobNames.Count
                    || DateTime.UtcNow - status.LastLog >= TimeSpan.FromMinutes(5);
                if (logStatus)
                {
                    _reporter.LogPollStatus(snapshot);
                    status = new PollStatus(snapshot.StageJobs.Count, snapshot.CompletedJobNames.Count, DateTime.UtcNow);
                }

                // Azure DevOps gates on the whole stage, while Helix gates only on the current
                // attempt. Previous-attempt work has already been reconciled by RetryPlanner.
                bool pipelineComplete = HelixJobMonitorUtilities.AreNonMonitorJobsComplete(snapshot.TimelineRecords, _options.JobMonitorName);
                bool helixComplete = snapshot.StageJobs.Where(_poller.IsCurrentAttempt)
                    .All(job => snapshot.CompletedJobNames.Contains(job.JobName));
                if (pipelineComplete && helixComplete)
                {
                    // Uploads are outcome-independent, but normal completion gives every accepted
                    // upload a chance to reach its durable completion tag before exit.
                    await _uploads.CompleteAndDrainAsync(cancellationToken);
                    _reporter.LogFinalFailedWorkItems();
                    _reporter.LogFinalSummary(_ledger.AssociatedJobsCount);

                    // Preserve the external failure precedence: pipeline failures first, then a
                    // missing submission, then the latest logical Helix work-item outcomes.
                    if (HelixJobMonitorUtilities.HasFailedNonMonitorJobs(snapshot.TimelineRecords, _options.JobMonitorName,
                        _ledger.SnapshotRetryingHelixSubmitterJobs()))
                    {
                        _reporter.LogNonMonitorPipelineFailure();
                        return 1;
                    }

                    if (_ledger.AssociatedJobsCount == 0 && !_options.AllowNoHelixJobs)
                    {
                        _reporter.LogNoHelixJobsFailure();
                        return 1;
                    }

                    return _ledger.HasFailedWorkItem ? 1 : 0;
                }

                await _delay(TimeSpan.FromSeconds(Math.Max(5, _options.PollingIntervalSeconds)), cancellationToken);
            }
        }

        private async Task ReconcileCompletedAsync(HelixJobInfo job,
            IReadOnlyCollection<Microsoft.DotNet.Helix.Client.Models.WorkItemSummary> workItems,
            CancellationToken cancellationToken)
        {
            if (_ledger.IsWorkItemOutcomesRecorded(job.JobName))
            {
                return;
            }

            bool processed = _ledger.IsHelixJobProcessed(job.JobName);
            if (!processed)
            {
                _reporter.LogJobProcessingStart(job);
                _reporter.LogFailedWorkItemConsoleLinks(job, workItems.Where(item => item.IsFailed));
            }

            // Outcome reconciliation is required even for a job uploaded by an earlier monitor;
            // upload deduplication is durable, but the pass/fail ledger is invocation-local.
            _ledger.TryRecordWorkItemOutcomes(job, workItems);
            if (!processed && _ledger.TryQueueHelixJobUpload(job.JobName))
            {
                await _uploads.EnqueueAsync(job, workItems, cancellationToken);
            }

            if (!processed)
            {
                _reporter.LogJobCompleted(job, workItems);
            }
        }

        private async Task CancelLatestInFlightAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<HelixJobInfo> jobs =
            [
                ..MonitorLedger.GetLatestHelixJobAttempts(_ledger.SnapshotAssociatedJobs())
                    .Where(job => !job.IsCompleted && !_ledger.IsHelixJobProcessed(job.JobName))
                    .OrderBy(job => job.JobName, StringComparer.OrdinalIgnoreCase)
            ];
            if (jobs.Count == 0)
            {
                return;
            }

            // Cancel only lineage leaves. Canceling superseded jobs adds noise and does not stop
            // the latest incarnation that currently owns the logical work.
            _logger.LogWarning(
                "##vso[task.logissue type=warning]Cancellation requested. Attempting to cancel {Count} in-flight Helix job(s).",
                jobs.Count);

            using var cancellations = new AsyncWorkQueue<HelixJobInfo>(
                Math.Min(8, jobs.Count),
                jobs.Count,
                async (job, token) =>
                {
                    try
                    {
                        await _helix.CancelJobAsync(job.JobName, token);
                        _logger.LogWarning("🛑 Requested cancellation of Helix job {JobName}.{nl}{JobUri}",
                            job.DisplayName, Environment.NewLine, job.DetailsUri);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(
                            exception,
                            "##vso[task.logissue type=warning]Failed to cancel Helix job {JobName}.",
                            job.DisplayName);
                    }
                });

            try
            {
                foreach (HelixJobInfo job in jobs)
                {
                    await cancellations.EnqueueAsync(job, cancellationToken);
                }

                await cancellations.CompleteAndDrainAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                cancellations.Abandon();
            }
        }

        public void Dispose()
        {
            _uploads.Dispose();
            (_azdo as IDisposable)?.Dispose();
            (_helix as IDisposable)?.Dispose();
        }

        private sealed record PollStatus(int JobCount = -1, int CompletedCount = -1, DateTime LastLog = default);
    }
}
