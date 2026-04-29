// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.Helix.JobMonitor.Models;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    internal sealed class JobMonitorRunner : IJobMonitorRunner, IDisposable
    {
        private readonly JobMonitorOptions _options;
        private readonly ILogger _logger;
        private readonly IAzureDevOpsService _azdo;
        private readonly IHelixService _helix;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayFunc;

        /// <summary>
        /// Constructor for production use with real services.
        /// </summary>
        public JobMonitorRunner(JobMonitorOptions options, ILogger logger)
            : this(options,
                  logger,
                  new AzureDevOpsService(options, logger),
                  new HelixService(options, logger),
                  null)
        {
        }

        /// <summary>
        /// Constructor for testing with injected services.
        /// </summary>
        internal JobMonitorRunner(
            JobMonitorOptions options,
            ILogger logger,
            IAzureDevOpsService azdo,
            IHelixService helix,
            Func<TimeSpan, CancellationToken, Task> delayFunc)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _azdo = azdo ?? throw new ArgumentNullException(nameof(azdo));
            _helix = helix ?? throw new ArgumentNullException(nameof(helix));
            _delayFunc = delayFunc ?? Task.Delay;
            Directory.CreateDirectory(_options.WorkingDirectory);
        }

        public Task<int> RunAsync(CancellationToken cancellationToken)
        {
            return RunCoreAsync(cancellationToken);
        }

        public async Task<int> RunAsync()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(_options.MaximumWaitMinutes));
            return await RunCoreAsync(cancellationTokenSource.Token);
        }

        private async Task<int> RunCoreAsync(CancellationToken cancellationToken)
        {
            if (_options.MonitorAllStages || string.IsNullOrEmpty(_options.StageName))
            {
                _logger.LogInformation("Monitoring Helix jobs for the pipeline");
            }
            else
            {
                _logger.LogInformation("Monitoring Helix jobs sent from stage '{StageName}'", _options.StageName);
            }

            IReadOnlySet<string> alreadyProcessed = await _azdo.GetProcessedHelixJobNamesAsync(cancellationToken);
            var processedHelixJobs = new HashSet<string>(alreadyProcessed, StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<HelixJobInfo> latestAssociatedJobs = [];

            try
            {
                return await RunLoopAsync(processedHelixJobs, latestJobs => latestAssociatedJobs = latestJobs, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ReportTimeout(latestAssociatedJobs, processedHelixJobs);
                return 1;
            }
        }

        /// <summary>
        /// Tracks the latest outcome for each logical work item, keyed by work item name.
        /// When a resubmission passes a previously-failed item, the outcome is updated.
        /// </summary>
        private readonly Dictionary<string, bool> _workItemOutcomes = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Tracks which work item names belong to which Helix job, so resubmission only
        /// resubmits items from the specific source job.
        /// </summary>
        private readonly Dictionary<string, HashSet<string>> _workItemsByJob = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Tracks which original Helix jobs have already had their failed work items resubmitted,
        /// so we don't resubmit twice for the same source job.
        /// </summary>
        private readonly HashSet<string> _resubmittedSourceJobs = new(StringComparer.OrdinalIgnoreCase);

        private bool IsRetryAttempt => _options.Attempt.GetValueOrDefault(1) > 1;

        private async Task<int> RunLoopAsync(
            HashSet<string> processedHelixJobs,
            Action<IReadOnlyList<HelixJobInfo>> reportLatestJobs,
            CancellationToken cancellationToken)
        {
            bool anyNonMonitorJobFailures = false;
            int processedHelixJobCount = 0;
            int allHelixJobCount = 0;
            int completedJobsCount = -1;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IReadOnlyList<AzureDevOpsTimelineRecord> timelineRecords = await _azdo.GetTimelineRecordsAsync(cancellationToken);
                IReadOnlyList<HelixJobInfo> associatedJobsWithBuild = await _helix.GetJobsAsync(cancellationToken);

                // When the monitor is scoped to a single stage, drop timeline records and Helix jobs
                // that belong to other stages so they don't gate completion or contribute failures.
                if (!_options.MonitorAllStages && !string.IsNullOrEmpty(_options.StageName))
                {
                    timelineRecords = HelixJobMonitorUtilities.FilterRecordsToStage(timelineRecords, _options.StageName);
                    associatedJobsWithBuild =
                    [
                        ..associatedJobsWithBuild.Where(j =>
                            string.IsNullOrEmpty(j.StageName)
                            || string.Equals(j.StageName, _options.StageName, StringComparison.OrdinalIgnoreCase))
                    ];
                }

                reportLatestJobs(associatedJobsWithBuild);

                // Filter jobs to completed ones belonging to this build
                IReadOnlyCollection<HelixJobInfo> completedJobs =
                [
                    ..associatedJobsWithBuild
                        .Where(j => j.IsCompleted)
                        .OrderBy(j => j.JobName, StringComparer.OrdinalIgnoreCase)
                ];

                if (allHelixJobCount != associatedJobsWithBuild.Count || completedJobsCount != completedJobs.Count)
                {
                    _logger.LogInformation("{CompletedCount}/{TotalCount} Helix jobs finished", completedJobs.Count, associatedJobsWithBuild.Count);
                    allHelixJobCount = associatedJobsWithBuild.Count;
                    completedJobsCount = completedJobs.Count;
                }

                bool resubmittedThisIteration = false;
                foreach (HelixJobInfo job in completedJobs.Where(j => !processedHelixJobs.Contains(j.JobName)))
                {
                    await ProcessCompletedJobAsync(job, cancellationToken);
                    processedHelixJobs.Add(job.JobName);
                    processedHelixJobCount++;

                    // On retry attempts, resubmit failed work items from this job
                    if (IsRetryAttempt)
                    {
                        resubmittedThisIteration |= await TryResubmitFailedWorkItemsAsync(job.JobName, cancellationToken);
                    }
                }

                anyNonMonitorJobFailures = HelixJobMonitorUtilities.HasFailedNonMonitorJobs(timelineRecords, _options.JobMonitorName);
                bool allPipelineJobsComplete = HelixJobMonitorUtilities.AreNonMonitorJobsComplete(timelineRecords, _options.JobMonitorName);
                bool allHelixJobsComplete = associatedJobsWithBuild.Count == 0 || associatedJobsWithBuild.All(j => j.IsCompleted);

                // If we just issued resubmissions, don't exit yet — wait for them to appear and complete.
                if (resubmittedThisIteration)
                {
                    await _delayFunc(TimeSpan.FromSeconds(Math.Max(5, _options.PollingIntervalSeconds)), cancellationToken);
                    continue;
                }

                if (allPipelineJobsComplete && allHelixJobsComplete)
                {
                    bool anyWorkItemFailed = _workItemOutcomes.Values.Any(passed => !passed);
                    _logger.LogInformation("Final summary: processed {ProcessedCount} Helix job(s); {FailedWorkItems} work item(s) failed.",
                        processedHelixJobCount, _workItemOutcomes.Values.Count(passed => !passed));

                    if (anyNonMonitorJobFailures || anyWorkItemFailed)
                    {
                        if (anyNonMonitorJobFailures)
                        {
                            _logger.LogError("One or more non-monitor pipeline jobs failed.");
                        }

                        if (anyWorkItemFailed)
                        {
                            var failedItems = _workItemOutcomes.Where(kv => !kv.Value).Select(kv => kv.Key).ToList();
                            _logger.LogError("The Helix Job Monitor detected {Count} failed work item(s): {Items}",
                                failedItems.Count, string.Join(", ", failedItems));
                        }

                        return 1;
                    }

                    return 0;
                }

                // If all pipeline jobs are dead and Helix jobs are still running,
                // those jobs are orphaned — no point waiting.
                if (allPipelineJobsComplete && anyNonMonitorJobFailures && !allHelixJobsComplete)
                {
                    _logger.LogError("All non-monitor pipeline jobs failed/canceled while Helix jobs are still running. Exiting.");
                    return 1;
                }

                await _delayFunc(TimeSpan.FromSeconds(Math.Max(5, _options.PollingIntervalSeconds)), cancellationToken);
            }
        }

        private async Task ProcessCompletedJobAsync(
            HelixJobInfo helixJob,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing completed job {jobName}...", helixJob.JobName);

            IReadOnlyCollection<WorkItemSummary> workItems = await _helix.ListWorkItemsAsync(helixJob.JobName, cancellationToken);

            // Update per-work-item outcome tracking
            var jobWorkItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (WorkItemSummary wi in workItems)
            {
                bool passed = wi.ExitCode == 0 && wi.State.Equals("Finished", StringComparison.OrdinalIgnoreCase);
                // A resubmission result overwrites a prior failure for the same work item name
                _workItemOutcomes[wi.Name] = passed;
                jobWorkItems.Add(wi.Name);
            }

            _workItemsByJob[helixJob.JobName] = jobWorkItems;

            int failedWorkItemCount = workItems.Count(wi => wi.ExitCode != 0 || !wi.State.Equals("Finished", StringComparison.OrdinalIgnoreCase));
            int successfulWorkItemCount = workItems.Count - failedWorkItemCount;

            int testRunId = await _azdo.CreateTestRunAsync(helixJob.TestRunName, helixJob.JobName, cancellationToken);

            try
            {
                IReadOnlyList<WorkItemTestResults> downloadedFiles = await _helix.DownloadTestResultsAsync(
                    helixJob.JobName,
                    [..workItems.Select(w => w.Name)],
                    cancellationToken);

                await _azdo.UploadTestResultsAsync(testRunId, downloadedFiles, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload test results for job {JobName} to Azure DevOps. Test run ID was {TestRunId}.", helixJob.JobName, testRunId);
            }
            finally
            {
                await _azdo.CompleteTestRunAsync(testRunId, cancellationToken);
            }

            _logger.LogInformation("Job '{JobName}' completed ({PassedCount} passed, {FailedCount} failed).", helixJob.JobName, successfulWorkItemCount, failedWorkItemCount);
        }

        /// <summary>
        /// Returns true if a resubmission was issued.
        /// </summary>
        private async Task<bool> TryResubmitFailedWorkItemsAsync(string jobName, CancellationToken cancellationToken)
        {
            // Don't resubmit from the same source job twice
            if (!_resubmittedSourceJobs.Add(jobName))
            {
                return false;
            }

            // Find work items FROM THIS JOB that are currently failed in the outcome map
            if (!_workItemsByJob.TryGetValue(jobName, out HashSet<string> jobItems))
            {
                return false;
            }

            var failedWorkItems = jobItems
                .Where(wi => _workItemOutcomes.TryGetValue(wi, out bool passed) && !passed)
                .ToList();

            if (failedWorkItems.Count == 0)
            {
                return false;
            }

            _logger.LogInformation("Resubmitting {Count} failed work item(s) from job '{JobName}': {Items}",
                failedWorkItems.Count, jobName, string.Join(", ", failedWorkItems));

            try
            {
                HelixJobInfo newJob = await _helix.ResubmitFailedWorkItemsAsync(jobName, failedWorkItems, cancellationToken);
                if (newJob != null)
                {
                    _logger.LogInformation("Resubmitted as new Helix job '{NewJobName}'.", newJob.JobName);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resubmit work items from job '{JobName}'.", jobName);
            }

            return false;
        }

        private void ReportTimeout(
            IReadOnlyList<HelixJobInfo> latestAssociatedJobs,
            HashSet<string> processedHelixJobs)
        {
            var timeout = TimeSpan.FromMinutes(_options.MaximumWaitMinutes);
            var unfinishedJobs = latestAssociatedJobs
                .Where(j => !j.IsCompleted || !processedHelixJobs.Contains(j.JobName))
                .OrderBy(j => j.JobName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (unfinishedJobs.Count == 0)
            {
                _logger.LogCritical("Helix Job Monitor timed out after {TimeoutMinutes} minute(s) ({Timeout}). No unfinished Helix jobs were tracked at the time of timeout.",
                    timeout.TotalMinutes,
                    timeout);
                return;
            }

            _logger.LogError(
                "Helix Job Monitor timed out after {TimeoutMinutes} minute(s) ({Timeout}). {UnfinishedCount} Helix job(s) had not finished: {UnfinishedJobs}",
                timeout.TotalMinutes,
                timeout,
                unfinishedJobs.Count,
                string.Join(", ", unfinishedJobs.Select(j => $"{j.JobName} (status: {j.Status})")));
        }

        public void Dispose()
        {
            (_azdo as IDisposable)?.Dispose();
            (_helix as IDisposable)?.Dispose();
        }
    }
}
