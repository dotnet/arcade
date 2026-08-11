// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.JobMonitor.ResultPublishing;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.Helix.JobMonitor.Models;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    /// <summary>
    /// Bounded, channel-backed processing for completed-job test-result publication. Individual
    /// upload failures are warnings by design; queue infrastructure failures remain observable.
    /// </summary>
    internal sealed class TestResultUploadPipeline : IDisposable
    {
        private const int DownloadAttempts = 3;
        private const string AzdoWarningPrefix = "##vso[task.logissue type=warning]";
        private readonly ILogger _logger;
        private readonly JobMonitorOptions _options;
        private readonly IAzureDevOpsService _azdo;
        private readonly IHelixService _helix;
        private readonly MonitorLedger _ledger;
        private readonly RetryExecutor _retry;
        private readonly AsyncWorkQueue<UploadRequest> _queue;
        private readonly ConcurrentDictionary<string, UploadRequest> _pending = new(StringComparer.OrdinalIgnoreCase);

        public TestResultUploadPipeline(
            ILogger logger,
            JobMonitorOptions options,
            IAzureDevOpsService azdo,
            IHelixService helix,
            MonitorLedger ledger)
        {
            _logger = logger;
            _options = options;
            _azdo = azdo;
            _helix = helix;
            _ledger = ledger;
            _retry = new RetryExecutor(logger);
            int parallelism = options.TestResultUploadParallelism;

            // Job-level concurrency is bounded independently from the service-wide work-item
            // publisher limit. The channel also prevents completed jobs from accumulating freely.
            _queue = new AsyncWorkQueue<UploadRequest>(
                parallelism,
                Math.Max(parallelism * 2, 1),
                ProcessAsync);
        }

        public async ValueTask EnqueueAsync(
            HelixJobInfo job,
            IReadOnlyCollection<WorkItemSummary> workItems,
            CancellationToken cancellationToken)
        {
            var request = new UploadRequest(job, [.. workItems.Select(item => item.Name)]);

            // Pending requests are diagnostic state only. Durable completion is represented by
            // the Azure DevOps tag and mirrored in MonitorLedger, never by this in-memory map.
            _pending[job.JobName] = request;
            try
            {
                await _queue.EnqueueAsync(request, cancellationToken);
            }
            catch
            {
                _pending.TryRemove(job.JobName, out _);
                throw;
            }
        }

        /// <summary>Completes input and drains uploads during normal monitor completion.</summary>
        public async Task CompleteAndDrainAsync(CancellationToken cancellationToken)
        {
            if (_pending.Count > 0)
            {
                _logger.LogInformation("Waiting for {Count} pending test result upload(s) to complete.", _pending.Count);
            }

            Task drain = _queue.CompleteAndDrainAsync(cancellationToken);
            if (_options.Verbose)
            {
                // Uploads can spend minutes parsing or publishing large result sets. Heartbeats
                // make a healthy drain distinguishable from a hung monitor without polling workers.
                TimeSpan heartbeat = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));
                while (!drain.IsCompleted)
                {
                    Task tick = Task.Delay(heartbeat, cancellationToken);
                    if (await Task.WhenAny(drain, tick) == drain)
                    {
                        break;
                    }

                    LogPending();
                }
            }

            await drain;
        }

        /// <summary>Immediately abandons queued/in-flight uploads during monitor cancellation.</summary>
        public void Abandon() => _queue.Abandon();

        public void Dispose() => _queue.Dispose();

        private async Task ProcessAsync(UploadRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await UploadAsync(request, cancellationToken);
            }
            finally
            {
                _pending.TryRemove(request.Job.JobName, out _);
            }
        }

        private async Task UploadAsync(UploadRequest request, CancellationToken cancellationToken)
        {
            HelixJobInfo job = request.Job;
            _ledger.MarkHelixJobUploadInProgress(job.JobName);

            // Downloads are read-only and safe to retry before creating any Azure DevOps state.
            request.SetPhase($"downloading Helix test results for {request.WorkItemNames.Count} work item(s)");
            IReadOnlyList<WorkItemTestResults> downloaded;
            try
            {
                downloaded = await _retry.ExecuteAsync(
                    token => _helix.DownloadTestResultsAsync(job.JobName, request.WorkItemNames, _options.WorkingDirectory, token),
                    "download Helix test results",
                    cancellationToken,
                    DownloadAttempts);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogUploadFailure(ex, "download the Helix test results", job, 0, retrySafe: true);
                _ledger.MarkHelixJobUploadFailed(job.JobName);
                return;
            }

            request.SetPhase("creating the Azure DevOps test run");
            int testRunId;
            try
            {
                // Creation is an ambiguous lifecycle write and is deliberately never retried.
                testRunId = await _azdo.CreateTestRunAsync(job.TestRunName, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogUploadFailure(ex, "create the Azure DevOps test run", job, 0, retrySafe: false);
                _ledger.MarkHelixJobUploadFailed(job.JobName);
                return;
            }

            IReadOnlyDictionary<(string JobName, string WorkItemName), TestResultUploadSummary> results;
            request.SetPhase($"publishing {downloaded.Count} work item(s) to Azure DevOps test run {testRunId}");
            try
            {
                // Publishing preserves existing one-shot semantics because it may partially write.
                results = await _azdo.UploadTestResultsAsync(testRunId, downloaded, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogUploadFailure(ex, "upload the test results to Azure DevOps", job, testRunId, retrySafe: false);
                _ledger.MarkHelixJobUploadFailed(job.JobName);
                return;
            }

            if (_options.FailWorkItemsWithFailedTests)
            {
                // Test frameworks can report a failure even when the Helix process exits zero.
                // Feed that signal into the same latest-incarnation outcome map as exit codes.
                _ledger.ObserveTestResults(results);
            }

            IReadOnlyCollection<string> failedWorkItems =
            [
                ..results.Where(pair => !pair.Value.AllPassed).Select(pair => pair.Key.WorkItemName)
            ];
            request.SetPhase($"completing and tagging Azure DevOps test run {testRunId}");
            try
            {
                // Completion is the durability boundary: failed-item retry metadata is attached
                // first, then the run is completed and tagged as fully processed.
                await _azdo.CompleteTestRunAsync(testRunId, job.JobName, failedWorkItems, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogUploadFailure(ex, "complete and tag the Azure DevOps test run", job, testRunId, retrySafe: false);
                _ledger.MarkHelixJobUploadFailed(job.JobName);
                return;
            }

            _ledger.TryMarkHelixJobProcessed(job.JobName);
            _logger.LogInformation("{UploadedCount} test results for job '{JobName}' processed.",
                results.Values.Sum(summary => summary.UploadedCount), job.DisplayName);
        }

        private void LogUploadFailure(Exception exception, string operation, HelixJobInfo job, int testRunId, bool retrySafe)
        {
            string replay = retrySafe
                ? "Transient reads were retried before this failure."
                : TransientFailureDetector.IsTransient(exception)
                    ? "The operation may have partially completed and is not safe to replay in this invocation."
                    : "The failure is not retryable.";
            _logger.LogWarning(exception,
                "{Prefix}Failed to {Operation} for job {JobName}. Test run ID was {TestRunId}. {Replay} "
                + "The run remains untagged and a later monitor invocation may retry the upload.",
                AzdoWarningPrefix, operation, job.DisplayName, testRunId, replay);
        }

        private sealed class UploadRequest
        {
            private readonly object _sync = new();
            private DateTimeOffset _phaseStartedAt = DateTimeOffset.UtcNow;

            public UploadRequest(HelixJobInfo job, IReadOnlyList<string> workItemNames)
            {
                Job = job;
                WorkItemNames = workItemNames;
            }

            public HelixJobInfo Job { get; }
            public IReadOnlyList<string> WorkItemNames { get; }
            public string Phase { get; private set; } = "queued";
            public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;
            public void SetPhase(string phase)
            {
                lock (_sync)
                {
                    Phase = phase;
                    _phaseStartedAt = DateTimeOffset.UtcNow;
                }
            }

            public (string Phase, DateTimeOffset StartedAt) GetPhase()
            {
                lock (_sync)
                {
                    return (Phase, _phaseStartedAt);
                }
            }
        }

        private void LogPending()
        {
            if (_pending.IsEmpty)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            string details = string.Join(Environment.NewLine, _pending.Values.OrderBy(request => request.StartedAt)
                .Select(request =>
                {
                    (string phase, DateTimeOffset phaseStartedAt) = request.GetPhase();
                    return $"- {request.Job.DisplayName}: phase='{phase}', phase elapsed={now - phaseStartedAt:c}, total elapsed={now - request.StartedAt:c}";
                }));
            _logger.LogDebug("{Count} test result upload(s) remain pending:{nl}{Details}",
                _pending.Count, Environment.NewLine, details);
        }
    }
}
