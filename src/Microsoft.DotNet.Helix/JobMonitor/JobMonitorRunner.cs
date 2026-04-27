// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

            bool anyNonMonitorJobFailures = false;
            int failedHelixJobCount = 0;
            int processedHelixJobCount = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                IReadOnlyList<AzureDevOpsTimelineRecord> timelineRecords = await _azdo.GetTimelineRecordsAsync(cancellationToken);
                IReadOnlyList<HelixJobInfo> jobs = await _helix.GetJobsAsync(cancellationToken);

                IReadOnlyCollection<HelixJobInfo> completedJobs = jobs
                    .Where(j => j.IsCompleted)
                    .OrderBy(j => j.JobName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _logger.LogInformation("{CompletedCount}/{TotalCount} Helix jobs finished", completedJobs.Count, jobs.Count);

                foreach (HelixJobInfo job in completedJobs.Where(j => !processedHelixJobs.Contains(j.JobName)))
                {
                    bool passed = await ProcessCompletedJobAsync(job, processedHelixJobs, cancellationToken);
                    processedHelixJobCount++;
                    if (!passed)
                    {
                        failedHelixJobCount++;
                    }
                }

                anyNonMonitorJobFailures = HelixJobMonitorUtilities.HasFailedNonMonitorJobs(timelineRecords, _options.JobMonitorName);
                bool allPipelineJobsComplete = HelixJobMonitorUtilities.AreNonMonitorJobsComplete(timelineRecords, _options.JobMonitorName);
                bool allHelixJobsComplete = jobs.Count == 0 || jobs.All(j => j.IsCompleted);

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
            HelixJobInfo job,
            HashSet<string> processedHelixJobs,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing completed job {jobName}...", job.JobName);

            string testRunName = job.TestRunName ?? job.JobName;
            int testRunId = await _azdo.CreateTestRunAsync(testRunName, job.JobName, cancellationToken);

            try
            {
                HelixJobPassFail passFail = await _helix.GetJobPassFailAsync(job.JobName, cancellationToken);
                IReadOnlyList<WorkItemTestResults> downloadedResults = await _helix.DownloadTestResultsAsync(
                    job.JobName, passFail, cancellationToken);

                bool allTestsPassed = await _azdo.UploadTestResultsAsync(testRunId, downloadedResults, cancellationToken);
                await _azdo.CompleteTestRunAsync(testRunId, cancellationToken);

                processedHelixJobs.Add(job.JobName);

                _logger.LogInformation("Job '{JobName}' completed ({PassedCount} passed, {FailedCount} failed).",
                    job.JobName, passFail.PassedWorkItems.Count, passFail.FailedWorkItems.Count);

                return !passFail.HasFailures && allTestsPassed
                    && !job.Status.Equals("failed", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process job '{JobName}'. Test run ID was {TestRunId}.", job.JobName, testRunId);
                // Don't add to processedHelixJobs — allows retry to pick it up.
                return false;
            }
        }

        public void Dispose()
        {
            (_azdo as IDisposable)?.Dispose();
            (_helix as IDisposable)?.Dispose();
        }
    }
}
