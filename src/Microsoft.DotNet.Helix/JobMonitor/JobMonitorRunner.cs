// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client;
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
        /// Tracks the latest outcome for each logical work item, keyed by work item name.
        /// When a resubmission passes a previously-failed item, the outcome is updated.
        /// </summary>
        private readonly Dictionary<string, bool> _workItemOutcomes = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _workItemOutcomeJobs = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Tracks which work item names belong to which Helix job, so resubmission only
        /// resubmits items from the specific source job.
        /// </summary>
        private readonly Dictionary<string, HashSet<string>> _workItemsByJob = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Constructor for production use with real services.
        /// </summary>
        public JobMonitorRunner(JobMonitorOptions options, ILogger logger)
            : this(options,
                  logger,
                  new AzureDevOpsService(options, logger),
                  new HelixService(string.IsNullOrEmpty(options.HelixAccessToken)
                      ? ApiFactory.GetAnonymous(options.HelixBaseUri)
                      : ApiFactory.GetAuthenticated(options.HelixBaseUri, options.HelixAccessToken),
                  logger),
                  delayFunc: null)
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
            HashSet<string> processedHelixJobs = new(alreadyProcessed, StringComparer.OrdinalIgnoreCase);
            HashSet<HelixJobInfo> associatedJobs = [];

            try
            {
                EntryResubmissionResult entryResubmission = await ResubmitFailedJobsAsync(cancellationToken);

                return await RunLoopAsync(
                    processedHelixJobs,
                    associatedJobs,
                    entryResubmission.RetryingHelixSubmitterJobs,
                    entryResubmission.JobsForFirstPoll,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                ReportTimeout(associatedJobs, processedHelixJobs);
                return 1;
            }
        }

        private async Task<int> RunLoopAsync(
            HashSet<string> processedHelixJobs,
            HashSet<HelixJobInfo> associatedJobs,
            HashSet<string> retryingHelixSubmitterJobs,
            IReadOnlyList<HelixJobInfo> jobsForFirstPoll,
            CancellationToken cancellationToken)
        {
            bool anyNonMonitorJobFailures = false;
            int processedHelixJobCount = 0;
            int allHelixJobCount = 0;
            int completedJobsCount = -1;
            DateTime lastPrintTime = DateTime.UtcNow;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IReadOnlyList<AzureDevOpsTimelineRecord> timelineRecords = await _azdo.GetTimelineRecordsAsync(cancellationToken);
                IReadOnlyList<HelixJobInfo> associatedJobsWithBuild = jobsForFirstPoll ?? await _helix.GetJobsForBuildAsync(
                    _options.Organization,
                    _options.RepositoryName,
                    _options.PrNumber,
                    _options.BuildId,
                    cancellationToken);
                jobsForFirstPoll = null;

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

                associatedJobs.UnionWith(associatedJobsWithBuild);

                // Filter jobs to completed ones belonging to this build
                IReadOnlyCollection<HelixJobInfo> completedJobs =
                [
                    ..OrderHelixJobsOldToNew(associatedJobsWithBuild.Where(j => j.IsCompleted))
                ];

                if (allHelixJobCount != associatedJobsWithBuild.Count
                    || completedJobsCount != completedJobs.Count
                    || (DateTime.UtcNow - lastPrintTime) >= TimeSpan.FromMinutes(5))
                {
                    _logger.LogInformation("Processed {ProcessedCount} / Completed {CompletedCount} / Total {TotalCount} Helix jobs",
                        processedHelixJobCount,
                        completedJobs.Count,
                        associatedJobsWithBuild.Count);
                    allHelixJobCount = associatedJobsWithBuild.Count;
                    completedJobsCount = completedJobs.Count;
                    lastPrintTime = DateTime.UtcNow;
                }

                foreach (HelixJobInfo job in completedJobs.Where(j => !processedHelixJobs.Contains(j.JobName)))
                {
                    await ProcessCompletedJobAsync(job, uploadTestResults: true, cancellationToken);
                    processedHelixJobs.Add(job.JobName);
                    processedHelixJobCount++;
                }

                foreach (HelixJobInfo job in OrderHelixJobsOldToNew(GetLatestHelixJobAttempts(associatedJobsWithBuild).Where(j => j.IsCompleted)))
                {
                    await ProcessCompletedJobAsync(job, uploadTestResults: false, cancellationToken);
                }

                anyNonMonitorJobFailures = HelixJobMonitorUtilities.HasFailedNonMonitorJobs(
                    timelineRecords,
                    _options.JobMonitorName,
                    retryingHelixSubmitterJobs);
                bool allPipelineJobsComplete = HelixJobMonitorUtilities.AreNonMonitorJobsComplete(timelineRecords, _options.JobMonitorName);
                bool allHelixJobsComplete = associatedJobsWithBuild.Count == 0 || associatedJobsWithBuild.All(j => j.IsCompleted);

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

                await Delay(cancellationToken);
            }
        }

        private async Task ProcessCompletedJobAsync(
            HelixJobInfo helixJob,
            bool uploadTestResults,
            CancellationToken cancellationToken)
        {
            if (!uploadTestResults && _workItemOutcomeJobs.Contains(helixJob.JobName))
            {
                return;
            }

            _logger.LogInformation("Job {jobName} completed. Processing test results...", helixJob.JobName);

            IReadOnlyCollection<WorkItemSummary> workItems = await _helix.ListWorkItemsAsync(helixJob.JobName, cancellationToken);

            // Update per-work-item outcome tracking
            if (_workItemOutcomeJobs.Add(helixJob.JobName))
            {
                var jobWorkItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (WorkItemSummary wi in workItems)
                {
                    // A resubmission result overwrites a prior failure for the same work item name
                    _workItemOutcomes[wi.Name] = !wi.IsFailed;
                    jobWorkItems.Add(wi.Name);
                }

                _workItemsByJob[helixJob.JobName] = jobWorkItems;
            }

            int failedWorkItemCount = workItems.Count(wi => wi.IsFailed);
            int successfulWorkItemCount = workItems.Count - failedWorkItemCount;

            if (uploadTestResults)
            {
                int testRunId = await _azdo.CreateTestRunAsync(helixJob.TestRunName, helixJob.JobName, cancellationToken);

                try
                {
                    IReadOnlyList<WorkItemTestResults> downloadedFiles = await _helix.DownloadTestResultsAsync(
                        helixJob.JobName,
                        [.. workItems.Select(w => w.Name)],
                        _options.WorkingDirectory, cancellationToken);

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
            }

            _logger.LogInformation("Job '{JobName}' completed ({PassedCount} passed, {FailedCount} failed).", helixJob.JobName, successfulWorkItemCount, failedWorkItemCount);
        }

        private async Task<EntryResubmissionResult> ResubmitFailedJobsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Checking for failed Helix work items to resubmit on monitor entry...");

            var retryingHelixSubmitterJobs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resubmittedJobs = new List<HelixJobInfo>();

            // This snapshot is taken when the monitor starts. Failed latest work items here are
            // not retried again until the monitor starts again, even if they fail during this run.
            IReadOnlyList<HelixJobInfo> allJobs = await _helix.GetJobsForBuildAsync(_options.Organization, _options.RepositoryName, _options.PrNumber, _options.BuildId, cancellationToken);
            IReadOnlyList<HelixJobInfo> scopedJobs =
            [
                ..allJobs.Where(IsHelixJobInScope)
            ];
            IReadOnlyList<HelixJobInfo> latestJobs = GetLatestHelixJobAttempts(scopedJobs);
            List<HelixJobInfo> completedHelixJobs =
            [
                ..latestJobs.Where(j => j.IsCompleted && IsHelixJobInScope(j))
            ];

            foreach (HelixJobInfo completedJob in completedHelixJobs)
            {
                IReadOnlyCollection<WorkItemSummary> workItems = await _helix.ListWorkItemsAsync(completedJob.JobName, cancellationToken);
                IReadOnlyCollection<WorkItemSummary> failedWorkItems = [..workItems.Where(wi => wi.IsFailed)];

                if (failedWorkItems.Count > 0)
                {
                    _logger.LogInformation("Resubmitting {Count} failed work item(s) for job {JobName}: {WorkItems}",
                        failedWorkItems.Count, completedJob.JobName, string.Join(Environment.NewLine + "- ", failedWorkItems.Select(wi => wi.Name)));
                    HelixJobInfo resubmittedJob = await _helix.ResubmitWorkItemsAsync(completedJob.JobName, failedWorkItems, cancellationToken);
                    if (resubmittedJob != null)
                    {
                        resubmittedJobs.Add(resubmittedJob);

                        if (!string.IsNullOrEmpty(completedJob.SubmitterJobName))
                        {
                            retryingHelixSubmitterJobs.Add(completedJob.SubmitterJobName);
                        }
                    }
                }
            }

            if (resubmittedJobs.Count == 0)
            {
                _logger.LogInformation("No failed jobs found to resubmit");
            }

            return new EntryResubmissionResult(
                retryingHelixSubmitterJobs,
                [..scopedJobs, ..resubmittedJobs]);
        }

        private bool IsHelixJobInScope(HelixJobInfo job)
        {
            return _options.MonitorAllStages
                || string.IsNullOrEmpty(_options.StageName)
                || string.IsNullOrEmpty(job.StageName)
                || string.Equals(job.StageName, _options.StageName, StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<HelixJobInfo> GetLatestHelixJobAttempts(IEnumerable<HelixJobInfo> jobs)
        {
            var supersededJobNames = new HashSet<string>(
                jobs
                    .Select(j => j.PreviousHelixJobName)
                    .Where(previousJobName => !string.IsNullOrEmpty(previousJobName)),
                StringComparer.OrdinalIgnoreCase);

            return
            [
                ..jobs.Where(j => !supersededJobNames.Contains(j.JobName))
            ];
        }

        private static IReadOnlyList<HelixJobInfo> OrderHelixJobsOldToNew(IEnumerable<HelixJobInfo> jobs)
        {
            var jobByName = jobs.ToDictionary(j => j.JobName, StringComparer.OrdinalIgnoreCase);
            return
            [
                ..jobs
                    .OrderBy(j => GetHelixJobLineageDepth(j, jobByName))
                    .ThenBy(j => j.JobName, StringComparer.OrdinalIgnoreCase)
            ];
        }

        private static int GetHelixJobLineageDepth(HelixJobInfo job, Dictionary<string, HelixJobInfo> jobByName)
        {
            int depth = 0;
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (!string.IsNullOrEmpty(job.PreviousHelixJobName)
                && visited.Add(job.PreviousHelixJobName)
                && jobByName.TryGetValue(job.PreviousHelixJobName, out job))
            {
                depth++;
            }

            return depth;
        }

        private sealed record EntryResubmissionResult(
            HashSet<string> RetryingHelixSubmitterJobs,
            IReadOnlyList<HelixJobInfo> JobsForFirstPoll);

        private void ReportTimeout(
            IEnumerable<HelixJobInfo> latestAssociatedJobs,
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

        private Task Delay(CancellationToken cancellationToken)
            => _delayFunc(TimeSpan.FromSeconds(Math.Max(5, _options.PollingIntervalSeconds)), cancellationToken);
    }
}

static file class WorkItemExtensions
{
    extension(WorkItemSummary workItem)
    {
        public bool IsFailed => workItem.ExitCode != 0 || !workItem.State.Equals("Finished", StringComparison.OrdinalIgnoreCase);
    }
}
