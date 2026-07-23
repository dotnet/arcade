// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.Helix.JobMonitor.Models;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    /// <summary>
    /// Fire-and-forget queue for AzDO test-result uploads. Each queued upload runs as an
    /// independent task with indefinite retry on transient errors. On normal completion the
    /// queue is drained so results in flight when the runner exits are not lost. On
    /// cancellation the queue is intentionally NOT drained: cancelling the in-flight Helix jobs
    /// takes priority, and any unfinished upload is re-uploaded in full by a later monitor
    /// invocation (a Helix job is only "processed" once its test run reaches the Completed state).
    /// </summary>
    internal sealed class TestResultUploadQueue
    {
        private const int MaximumTransientAttempts = 3;
        private const string AzdoWarningPrefix = "##vso[task.logissue type=warning]";

        private readonly ILogger _logger;
        private readonly JobMonitorOptions _options;
        private readonly IAzureDevOpsService _azdo;
        private readonly IHelixService _helix;
        private readonly MonitorState _monitorState;
        private readonly Func<CancellationToken, Task> _delay;
        private readonly List<Task> _pending = [];

        public TestResultUploadQueue(
            ILogger logger,
            JobMonitorOptions options,
            IAzureDevOpsService azdo,
            IHelixService helix,
            MonitorState monitorState,
            Func<CancellationToken, Task> delay)
        {
            _logger = logger;
            _options = options;
            _azdo = azdo;
            _helix = helix;
            _monitorState = monitorState;
            _delay = delay;
        }

        public void Enqueue(HelixJobInfo helixJob, IReadOnlyCollection<WorkItemSummary> workItems, CancellationToken cancellationToken)
        {
            IReadOnlyList<string> workItemNames = [.. workItems.Select(w => w.Name)];
            // Scheduling uses CancellationToken.None so the upload task is always allowed to start.
            // The upload body still observes the runner's token, so when the runner is cancelled the
            // upload stops promptly and the job's results are re-uploaded by a later invocation.
            Task uploadTask = Task.Run(() => UploadAsync(helixJob, workItemNames, cancellationToken), CancellationToken.None);
            _pending.Add(uploadTask);
        }

        public void Prune()
        {
            _pending.RemoveAll(static task => task.IsCompleted);
        }

        public async Task DrainAsync(CancellationToken cancellationToken)
        {
            Prune();
            if (_pending.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Waiting for {Count} pending test result upload(s) to complete.", _pending.Count);
            try
            {
                await Task.WhenAll(_pending).WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // One or more upload tasks were cancelled; treat as best-effort drained.
            }
            Prune();
        }

        private async Task UploadAsync(
            HelixJobInfo helixJob,
            IReadOnlyCollection<string> workItemNames,
            CancellationToken cancellationToken)
        {
            _monitorState.MarkHelixJobUploadInProgress(helixJob.JobName);

            (bool downloadedSuccessfully, IReadOnlyList<WorkItemTestResults> downloaded) = await TryExecuteWithRetryAsync(
                () => _helix.DownloadTestResultsAsync(
                        helixJob.JobName,
                        workItemNames,
                        _options.WorkingDirectory,
                        cancellationToken),
                "download the Helix test results",
                helixJob,
                testRunId: 0,
                safeToRetry: true,
                cancellationToken);
            if (!downloadedSuccessfully)
            {
                _monitorState.MarkHelixJobUploadFailed(helixJob.JobName);
                return;
            }

            (bool created, int testRunId) = await TryExecuteWithRetryAsync(
                () => _azdo.CreateTestRunAsync(helixJob.TestRunName, cancellationToken),
                "create the Azure DevOps test run",
                helixJob,
                testRunId: 0,
                safeToRetry: false,
                cancellationToken);
            if (!created)
            {
                _monitorState.MarkHelixJobUploadFailed(helixJob.JobName);
                return;
            }

            (bool uploadedSuccessfully, IReadOnlyDictionary<(string JobName, string WorkItemName), TestResultUploadSummary> testResults)
                = await TryExecuteWithRetryAsync(
                    () => _azdo.UploadTestResultsAsync(testRunId, downloaded, cancellationToken),
                    "upload the test results to Azure DevOps",
                    helixJob,
                    testRunId,
                    safeToRetry: false,
                    cancellationToken);
            if (!uploadedSuccessfully)
            {
                _monitorState.MarkHelixJobUploadFailed(helixJob.JobName);
                return;
            }

            if (_options.FailWorkItemsWithFailedTests)
            {
                _monitorState.ObserveTestResults(testResults);
            }

            IReadOnlyCollection<string> failedWorkItems =
            [
                .. testResults
                    .Where(kv => !kv.Value.AllPassed)
                    .Select(kv => kv.Key.WorkItemName)
            ];

            (bool completed, _) = await TryExecuteWithRetryAsync(
                async () =>
                {
                    await _azdo.CompleteTestRunAsync(testRunId, helixJob.JobName, failedWorkItems, cancellationToken);
                    return true;
                },
                "complete and tag the Azure DevOps test run",
                helixJob,
                testRunId,
                safeToRetry: false,
                cancellationToken);
            if (!completed)
            {
                _monitorState.MarkHelixJobUploadFailed(helixJob.JobName);
                return;
            }

            _monitorState.TryMarkHelixJobProcessed(helixJob.JobName);

            long uploadedCount = testResults.Values.Sum(r => r.UploadedCount);
            _logger.LogInformation("{UploadedCount} test results for job '{JobName}' processed.",
                uploadedCount,
                helixJob.DisplayName);
        }

        private async Task<(bool Success, T Result)> TryExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            string operationDescription,
            HelixJobInfo helixJob,
            int testRunId,
            bool safeToRetry,
            CancellationToken cancellationToken)
        {
            int maximumAttempts = safeToRetry ? MaximumTransientAttempts : 1;
            for (int attempt = 1; attempt <= maximumAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return (true, await operation());
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (TransientFailureDetector.IsTransient(ex) && attempt < maximumAttempts)
                {
                    _logger.LogDebug(ex,
                        "Failed to {OperationDescription} for job {JobName}. Test run ID was {TestRunId}. "
                        + "Transient attempt {Attempt} of {MaximumAttempts} failed; retrying after delay.",
                        operationDescription,
                        helixJob.DisplayName,
                        testRunId,
                        attempt,
                        maximumAttempts);
                    await _delay(cancellationToken);
                }
                catch (Exception ex)
                {
                    string failureKind = TransientFailureDetector.IsTransient(ex)
                        ? safeToRetry
                            ? "Transient retry limit reached."
                            : "The operation may have partially completed and is not safe to replay in this invocation."
                        : "The failure is not retryable.";
                    _logger.LogWarning(ex,
                        "{Prefix}Failed to {OperationDescription} for job {JobName}. Test run ID was {TestRunId}. "
                        + "{FailureKind} The run remains untagged and a later monitor invocation may retry the upload.",
                        AzdoWarningPrefix,
                        operationDescription,
                        helixJob.DisplayName,
                        testRunId,
                        failureKind);
                    return (false, default);
                }
            }

            throw new InvalidOperationException("Upload retry loop exited unexpectedly.");
        }

    }
}
