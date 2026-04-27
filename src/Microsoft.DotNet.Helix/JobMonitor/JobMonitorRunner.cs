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
            IReadOnlySet<string> alreadyProcessed = await _azdo.GetProcessedHelixJobNamesAsync(cancellationToken);
            var processedHelixJobs = new HashSet<string>(alreadyProcessed, StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<HelixJobInfo> latestAssociatedJobs = Array.Empty<HelixJobInfo>();

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

        private async Task<int> RunLoopAsync(
            HashSet<string> processedHelixJobs,
            Action<IReadOnlyList<HelixJobInfo>> reportLatestJobs,
            CancellationToken cancellationToken)
        {
            bool anyNonMonitorJobFailures = false;
            int failedHelixJobCount = 0;
            int processedHelixJobCount = 0;
            int allHelixJobCount = 0;
            int completedJobsCount = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IReadOnlyList<AzureDevOpsTimelineRecord> timelineRecords = await _azdo.GetTimelineRecordsAsync(cancellationToken);
                IReadOnlyList<HelixJobInfo> associatedJobsWithBuild = await _helix.GetJobsAsync(cancellationToken);
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

                foreach (HelixJobInfo job in completedJobs.Where(j => !processedHelixJobs.Contains(j.JobName)))
                {
                    bool passed = await ProcessCompletedJobAsync(job, cancellationToken);
                    processedHelixJobs.Add(job.JobName);
                    processedHelixJobCount++;
                    if (!passed)
                    {
                        failedHelixJobCount++;
                    }
                }

                anyNonMonitorJobFailures = HelixJobMonitorUtilities.HasFailedNonMonitorJobs(timelineRecords, _options.JobMonitorName);
                bool allPipelineJobsComplete = HelixJobMonitorUtilities.AreNonMonitorJobsComplete(timelineRecords, _options.JobMonitorName);
                bool allHelixJobsComplete = associatedJobsWithBuild.Count == 0 || associatedJobsWithBuild.All(j => j.IsCompleted);

                if (allPipelineJobsComplete && allHelixJobsComplete)
                {
                    _logger.LogInformation("Final summary: processed {ProcessedCount} Helix job(s); {FailedCount} failed.", processedHelixJobCount, failedHelixJobCount);
                    if (anyNonMonitorJobFailures || failedHelixJobCount > 0)
                    {
                        if (anyNonMonitorJobFailures)
                        {
                            _logger.LogError("One or more non-monitor pipeline jobs failed.");
                        }

                        if (failedHelixJobCount > 0)
                        {
                            _logger.LogError("The Helix Job Monitor detected failures in {FailedCount} Helix job(s).", failedHelixJobCount);
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

        private async Task<bool> ProcessCompletedJobAsync(
            HelixJobInfo helixJob,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing completed job {jobName}...", helixJob.JobName);

            IReadOnlyCollection<WorkItemSummary> workItems = await _helix.ListWorkItemsAsync(helixJob.JobName, cancellationToken);

            int failedWorkItemCount = workItems.Count(wi => wi.ExitCode != 0 || !wi.State.Equals("Finished", StringComparison.OrdinalIgnoreCase));
            bool helixJobSuccessful = failedWorkItemCount == 0;
            int sucessfulWorkItemCount = workItems.Count - failedWorkItemCount;

            int testRunId = await _azdo.CreateTestRunAsync(helixJob.TestRunName, helixJob.JobName, cancellationToken);

            try
            {
                IReadOnlyList<WorkItemTestResults> downloadedFiles = await _helix.DownloadTestResultsAsync(
                    helixJob.JobName,
                    [..workItems.Select(w => w.Name)],
                    cancellationToken);

                if (!await _azdo.UploadTestResultsAsync(testRunId, downloadedFiles, cancellationToken))
                {
                    sucessfulWorkItemCount--;
                    failedWorkItemCount++;
                    helixJobSuccessful = false;
                }
            }
            catch (Exception ex)
            {
                // TODO: Handle better here
                _logger.LogError(ex, "Failed to upload test results for job {JobName} to Azure DevOps. Test run ID was {TestRunId}.", helixJob.JobName, testRunId);
                return false;
            }
            finally
            {
                await _azdo.CompleteTestRunAsync(testRunId, cancellationToken);
            }

            _logger.LogInformation("Job '{JobName}' completed ({PassedCount} passed, {FailedCount} failed).", helixJob.JobName, sucessfulWorkItemCount, failedWorkItemCount);
            return failedWorkItemCount == 0;
        }

        public void Dispose()
        {
            (_azdo as IDisposable)?.Dispose();
            (_helix as IDisposable)?.Dispose();
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
    }
}
