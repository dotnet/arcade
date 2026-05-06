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
        private readonly Dictionary<string, FailedWorkItemConsoleInfo> _failedWorkItemConsoleInfo = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _reportedFailedWorkItemConsoleLinks = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Task> _pendingTestResultUploads = [];

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
            _logger.LogInformation("Monitoring Helix jobs for stage {stage} of build {BuildId}:{nl}https://dev.azure.com/dnceng-public/public/_build/results?buildId={BuildId}",
                _options.StageName,
                _options.BuildId,
                Environment.NewLine,
                _options.BuildId);

            IReadOnlySet<string> alreadyProcessed = await _azdo.GetProcessedHelixJobNamesAsync(cancellationToken);
            HashSet<string> processedHelixJobs = new(alreadyProcessed, StringComparer.OrdinalIgnoreCase);
            HashSet<HelixJobInfo> associatedJobs = new(HelixJobInfo.ByJobNameComparer);

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

                // Drop timeline records and Helix jobs that belong to other stages so they don't
                // gate completion or contribute failures.
                timelineRecords = HelixJobMonitorUtilities.FilterRecordsToStage(timelineRecords, _options.StageName);
                associatedJobsWithBuild =
                [
                    ..associatedJobsWithBuild.Where(j =>
                        string.IsNullOrEmpty(j.StageName)
                        || string.Equals(j.StageName, _options.StageName, StringComparison.OrdinalIgnoreCase))
                ];

                associatedJobs.UnionWith(associatedJobsWithBuild);

                // Filter jobs to completed ones belonging to this build. Helix job summaries can
                // omit Finished for failed jobs even after all work items have terminal exit codes.
                IReadOnlyCollection<HelixJobInfo> completedJobs =
                    await GetCompletedHelixJobsAsync(associatedJobsWithBuild, cancellationToken);
                var completedJobNames = new HashSet<string>(
                    completedJobs.Select(j => j.JobName),
                    StringComparer.OrdinalIgnoreCase);

                bool shouldLogStatus = _options.Verbose
                    || allHelixJobCount != associatedJobsWithBuild.Count
                    || completedJobsCount != completedJobs.Count
                    || (DateTime.UtcNow - lastPrintTime) >= TimeSpan.FromMinutes(5);

                foreach (HelixJobInfo job in completedJobs.Where(j => !processedHelixJobs.Contains(j.JobName)))
                {
                    await ProcessCompletedJobAsync(job, uploadTestResults: true, cancellationToken);
                    processedHelixJobs.Add(job.JobName);
                    processedHelixJobCount++;
                }

                foreach (HelixJobInfo job in OrderHelixJobsOldToNew(GetLatestHelixJobAttempts(associatedJobsWithBuild).Where(j => completedJobNames.Contains(j.JobName))))
                {
                    await ProcessCompletedJobAsync(job, uploadTestResults: false, cancellationToken);
                }

                PruneCompletedTestResultUploads();

                if (shouldLogStatus)
                {
                    await LogHelixJobStatusAsync(associatedJobsWithBuild, completedJobNames, processedHelixJobs, cancellationToken);
                    allHelixJobCount = associatedJobsWithBuild.Count;
                    completedJobsCount = completedJobs.Count;
                    lastPrintTime = DateTime.UtcNow;
                }

                anyNonMonitorJobFailures = HelixJobMonitorUtilities.HasFailedNonMonitorJobs(
                    timelineRecords,
                    _options.JobMonitorName,
                    retryingHelixSubmitterJobs);
                bool allPipelineJobsComplete = HelixJobMonitorUtilities.AreNonMonitorJobsComplete(timelineRecords, _options.JobMonitorName);
                bool allHelixJobsComplete = associatedJobsWithBuild.Count == 0 || associatedJobsWithBuild.All(j => completedJobNames.Contains(j.JobName));

                if (allPipelineJobsComplete && allHelixJobsComplete)
                {
                    await WaitForPendingTestResultUploadsAsync(cancellationToken);
                    bool anyWorkItemFailed = _workItemOutcomes.Values.Any(passed => !passed);
                    _logger.LogInformation("Final summary: processed {ProcessedCount} Helix job(s); {FailedWorkItems} work item(s) failed.",
                        processedHelixJobCount, _workItemOutcomes.Values.Count(passed => !passed));
                    LogFinalFailedWorkItemConsoleInfo();

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

                await Delay(cancellationToken);
            }
        }

        private async Task<IReadOnlyCollection<HelixJobInfo>> GetCompletedHelixJobsAsync(
            IReadOnlyList<HelixJobInfo> jobs,
            CancellationToken cancellationToken)
        {
            var completedJobs = new List<HelixJobInfo>();

            foreach (HelixJobInfo job in jobs)
            {
                if (job.IsCompleted || await AreAllWorkItemsTerminalAsync(job, cancellationToken))
                {
                    completedJobs.Add(job);
                }
            }

            return OrderHelixJobsOldToNew(completedJobs);
        }

        private async Task LogHelixJobStatusAsync(
            IReadOnlyList<HelixJobInfo> jobs,
            HashSet<string> completedJobNames,
            HashSet<string> processedJobNames,
            CancellationToken cancellationToken)
        {
            List<HelixJobInfo> orderedJobs =
            [
                ..jobs
                    .OrderBy(job => job.JobName, StringComparer.OrdinalIgnoreCase)
            ];
            var workItemsByJob = new Dictionary<string, IReadOnlyCollection<WorkItemSummary>>(StringComparer.OrdinalIgnoreCase);

            foreach (HelixJobInfo job in orderedJobs)
            {
                IReadOnlyCollection<WorkItemSummary> workItems = await _helix.ListWorkItemsAsync(job.JobName, cancellationToken);
                LogFailedWorkItemConsoleLinks(job, [..workItems.Where(IsFailedWorkItem)]);
                workItemsByJob[job.JobName] = workItems;
            }

            JobWorkItemStatusCounts counts = GetStatusCounts(orderedJobs, workItemsByJob, completedJobNames, processedJobNames);
            _logger.LogInformation("ℹ️ Jobs (Work Items) Status: {ProcessedJobs} ({ProcessedWorkItems}) processed / {CompletedJobs} ({CompletedWorkItems}) completed / {RunningJobs} ({RunningWorkItems}) running / {WaitingJobs} ({WaitingWorkItems}) waiting",
                counts.ProcessedJobs,
                counts.ProcessedWorkItems,
                counts.CompletedJobs,
                counts.CompletedWorkItems,
                counts.RunningJobs,
                counts.RunningWorkItems,
                counts.WaitingJobs,
                counts.WaitingWorkItems);

            if (_options.Verbose)
            {
                LogVerboseHelixJobStatus(orderedJobs, workItemsByJob, completedJobNames, processedJobNames);
            }
        }

        private static JobWorkItemStatusCounts GetStatusCounts(
            IReadOnlyList<HelixJobInfo> jobs,
            IReadOnlyDictionary<string, IReadOnlyCollection<WorkItemSummary>> workItemsByJob,
            HashSet<string> completedJobNames,
            HashSet<string> processedJobNames)
        {
            int processedJobs = 0;
            int processedWorkItems = 0;
            int completedJobs = 0;
            int completedWorkItems = 0;
            int runningJobs = 0;
            int runningWorkItems = 0;
            int waitingJobs = 0;
            int waitingWorkItems = 0;

            foreach (HelixJobInfo job in jobs)
            {
                IReadOnlyCollection<WorkItemSummary> workItems = workItemsByJob[job.JobName];

                if (processedJobNames.Contains(job.JobName))
                {
                    processedJobs++;
                    processedWorkItems += workItems.Count;
                }

                if (completedJobNames.Contains(job.JobName))
                {
                    completedJobs++;
                    completedWorkItems += workItems.Count;
                }
                else if (workItems.Count > 0)
                {
                    runningJobs++;
                    runningWorkItems += workItems.Count;
                }
                else
                {
                    waitingJobs++;
                    waitingWorkItems += job.InitialWorkItemCount ?? 0;
                }
            }

            return new JobWorkItemStatusCounts(
                processedJobs,
                processedWorkItems,
                completedJobs,
                completedWorkItems,
                runningJobs,
                runningWorkItems,
                waitingJobs,
                waitingWorkItems);
        }

        private void LogVerboseHelixJobStatus(
            IReadOnlyList<HelixJobInfo> jobs,
            IReadOnlyDictionary<string, IReadOnlyCollection<WorkItemSummary>> workItemsByJob,
            HashSet<string> completedJobNames,
            HashSet<string> processedJobNames)
        {
            if (jobs.Count == 0)
            {
                _logger.LogInformation("⏳ Helix job details:{nl}└─ no Helix jobs discovered yet", Environment.NewLine);
                return;
            }

            var lines = new List<string>();
            for (int jobIndex = 0; jobIndex < jobs.Count; jobIndex++)
            {
                HelixJobInfo job = jobs[jobIndex];
                IReadOnlyCollection<WorkItemSummary> workItems = workItemsByJob[job.JobName];
                AddVerboseJobLines(lines, job, workItems, GetJobStatus(job, workItems, completedJobNames, processedJobNames), isLastJob: jobIndex == jobs.Count - 1);
            }

            _logger.LogInformation("⏳ Helix job details:{nl}{JobDetails}",
                Environment.NewLine,
                string.Join(Environment.NewLine, lines));
        }

        private static void AddVerboseJobLines(
            List<string> lines,
            HelixJobInfo job,
            IReadOnlyCollection<WorkItemSummary> workItems,
            string jobStatus,
            bool isLastJob)
        {
            string jobConnector = isLastJob ? "└─" : "├─";
            string childPrefix = isLastJob ? "   " : "│  ";
            lines.Add($"{jobConnector} 🧪 Helix job {job.JobName} [{jobStatus}]");

            List<WorkItemSummary> orderedWorkItems =
            [
                ..workItems.OrderBy(wi => wi.Name, StringComparer.OrdinalIgnoreCase)
            ];

            if (orderedWorkItems.Count == 0)
            {
                lines.Add($"{childPrefix}└─ no work items reported yet");
                return;
            }

            for (int workItemIndex = 0; workItemIndex < orderedWorkItems.Count; workItemIndex++)
            {
                WorkItemSummary workItem = orderedWorkItems[workItemIndex];
                string workItemConnector = workItemIndex == orderedWorkItems.Count - 1 ? "└─" : "├─";
                string console = IsFailedWorkItem(workItem)
                    ? $" | Console: {GetConsoleOutputText(workItem.ConsoleOutputUri)}"
                    : string.Empty;
                lines.Add($"{childPrefix}{workItemConnector} {workItem.Name} ({FormatWorkItemState(workItem)}){console}");
            }
        }

        private static string GetJobStatus(
            HelixJobInfo job,
            IReadOnlyCollection<WorkItemSummary> workItems,
            HashSet<string> completedJobNames,
            HashSet<string> processedJobNames)
        {
            if (processedJobNames.Contains(job.JobName))
            {
                return "Processed";
            }

            if (completedJobNames.Contains(job.JobName))
            {
                return "Completed";
            }

            return workItems.Count > 0 ? "Running" : "Waiting";
        }

        private static string FormatWorkItemState(WorkItemSummary workItem)
        {
            string exitCode = workItem.ExitCode.HasValue ? $", exit code {workItem.ExitCode.Value}" : string.Empty;
            return $"{workItem.State}{exitCode}";
        }

        private async Task<bool> AreAllWorkItemsTerminalAsync(
            HelixJobInfo job,
            CancellationToken cancellationToken)
        {
            if (job.InitialWorkItemCount is not > 0)
            {
                return false;
            }

            IReadOnlyCollection<WorkItemSummary> workItems = await _helix.ListWorkItemsAsync(job.JobName, cancellationToken);
            return workItems.Count >= job.InitialWorkItemCount.Value
                && workItems.All(wi => wi.ExitCode.HasValue);
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

            _logger.LogInformation("Job {jobName} completed. Processing test results...{nl}{JobUri}", helixJob.JobName, Environment.NewLine, helixJob.DetailsUri);

            IReadOnlyCollection<WorkItemSummary> workItems = await _helix.ListWorkItemsAsync(helixJob.JobName, cancellationToken);
            LogFailedWorkItemConsoleLinks(helixJob, [..workItems.Where(wi => wi.IsFailed)]);

            // Update per-work-item outcome tracking
            if (_workItemOutcomeJobs.Add(helixJob.JobName))
            {
                var jobWorkItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (WorkItemSummary wi in workItems)
                {
                    // A resubmission result overwrites a prior failure for the same work item name
                    _workItemOutcomes[wi.Name] = !wi.IsFailed;
                    TrackFailedWorkItemConsoleInfo(helixJob, wi);
                    jobWorkItems.Add(wi.Name);
                }

                _workItemsByJob[helixJob.JobName] = jobWorkItems;
            }

            int failedWorkItemCount = workItems.Count(wi => wi.IsFailed);
            int successfulWorkItemCount = workItems.Count - failedWorkItemCount;

            if (uploadTestResults)
            {
                QueueTestResultUpload(helixJob, workItems, cancellationToken);
            }

            _logger.LogInformation("{Icon} Job '{JobName}' {Status} ({PassedCount} passed, {FailedCount} failed){nl}{JobUri}",
                failedWorkItemCount == 0 ? "✅" : "❌",
                helixJob.JobName,
                failedWorkItemCount == 0 ? "succeeded" : "failed",
                successfulWorkItemCount,
                failedWorkItemCount,
                Environment.NewLine, helixJob.DetailsUri);
        }

        private void QueueTestResultUpload(
            HelixJobInfo helixJob,
            IReadOnlyCollection<WorkItemSummary> workItems,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<string> workItemNames = [.. workItems.Select(w => w.Name)];
            Task uploadTask = Task.Run(
                () => UploadTestResultsForJobAsync(helixJob, workItemNames, cancellationToken),
                CancellationToken.None);
            _pendingTestResultUploads.Add(uploadTask);
        }

        private async Task UploadTestResultsForJobAsync(
            HelixJobInfo helixJob,
            IReadOnlyCollection<string> workItemNames,
            CancellationToken cancellationToken)
        {
            int testRunId = 0;

            try
            {
                testRunId = await _azdo.CreateTestRunAsync(helixJob.TestRunName, helixJob.JobName, cancellationToken);
                IReadOnlyList<WorkItemTestResults> downloadedFiles = await _helix.DownloadTestResultsAsync(
                    helixJob.JobName,
                    workItemNames,
                    _options.WorkingDirectory,
                    cancellationToken);

                int uploadedCount = await _azdo.UploadTestResultsAsync(testRunId, downloadedFiles, cancellationToken);
                _logger.LogInformation("{UploadedCount} test results for job '{JobName}' processed after upload.",
                    uploadedCount,
                    helixJob.JobName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload test results for job {JobName} to Azure DevOps. Test run ID was {TestRunId}.", helixJob.JobName, testRunId);
            }
            finally
            {
                if (testRunId != 0)
                {
                    try
                    {
                        await _azdo.CompleteTestRunAsync(testRunId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to complete Azure DevOps test run {TestRunId} for job {JobName}.", testRunId, helixJob.JobName);
                    }
                }
            }
        }

        private void PruneCompletedTestResultUploads()
        {
            _pendingTestResultUploads.RemoveAll(static task => task.IsCompleted);
        }

        private async Task WaitForPendingTestResultUploadsAsync(CancellationToken cancellationToken)
        {
            PruneCompletedTestResultUploads();
            if (_pendingTestResultUploads.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Waiting for {Count} pending test result upload(s) to complete.", _pendingTestResultUploads.Count);
            await Task.WhenAll(_pendingTestResultUploads);
            PruneCompletedTestResultUploads();
        }

        private void LogFailedWorkItemConsoleLinks(HelixJobInfo helixJob, IReadOnlyCollection<WorkItemSummary> workItems)
        {
            foreach (WorkItemSummary workItem in workItems.OrderBy(wi => wi.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!_reportedFailedWorkItemConsoleLinks.Add(GetWorkItemKey(helixJob.JobName, workItem.Name)))
                {
                    continue;
                }

                _logger.LogInformation("❌ Work item '{WorkItemName}' in job '{JobName}' failed ({State}).{nl}Console: {ConsoleOutputUri}",
                    workItem.Name,
                    helixJob.JobName,
                    FormatWorkItemState(workItem),
                    Environment.NewLine,
                    GetConsoleOutputText(workItem.ConsoleOutputUri));
            }
        }

        private static bool IsUnfinishedWorkItem(WorkItemSummary workItem)
            => !workItem.ExitCode.HasValue
                && !string.Equals(workItem.State, "Finished", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(workItem.State, "Failed", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(workItem.State, "TimedOut", StringComparison.OrdinalIgnoreCase);

        private static bool IsFailedWorkItem(WorkItemSummary workItem)
            => workItem.IsFailed && !IsUnfinishedWorkItem(workItem);

        private sealed record JobWorkItemStatusCounts(
            int ProcessedJobs,
            int ProcessedWorkItems,
            int CompletedJobs,
            int CompletedWorkItems,
            int RunningJobs,
            int RunningWorkItems,
            int WaitingJobs,
            int WaitingWorkItems);

        private static string GetWorkItemKey(string jobName, string workItemName)
            => $"{jobName}/{workItemName}";

        private void TrackFailedWorkItemConsoleInfo(HelixJobInfo helixJob, WorkItemSummary workItem)
        {
            if (workItem.IsFailed)
            {
                _failedWorkItemConsoleInfo[workItem.Name] = new FailedWorkItemConsoleInfo(
                    helixJob.JobName,
                    workItem.Name,
                    FormatWorkItemState(workItem),
                    GetConsoleOutputText(workItem.ConsoleOutputUri));
            }
            else
            {
                _failedWorkItemConsoleInfo.Remove(workItem.Name);
            }
        }

        private void LogFinalFailedWorkItemConsoleInfo()
        {
            if (_failedWorkItemConsoleInfo.Count == 0)
            {
                return;
            }

            List<FailedWorkItemConsoleInfo> failures =
            [
                .._failedWorkItemConsoleInfo.Values
                    .OrderBy(failure => failure.WorkItemName, StringComparer.OrdinalIgnoreCase)
            ];

            var lines = new List<string>();
            for (int i = 0; i < failures.Count; i++)
            {
                FailedWorkItemConsoleInfo failure = failures[i];
                string connector = i == failures.Count - 1 ? "└─" : "├─";
                lines.Add($"{connector} {failure.WorkItemName} (Job: {failure.JobName}) ({failure.State})");
                lines.Add($"{(i == failures.Count - 1 ? "   " : "│  ")}└─ Console: {failure.ConsoleOutput}");
            }

            _logger.LogError("❌ Failed work item console logs:{nl}Test results: {TestResultsUri}{nl}{FailedWorkItemConsoleLogs}",
                Environment.NewLine,
                GetTestResultsUri(),
                Environment.NewLine,
                string.Join(Environment.NewLine, lines));
        }

        private string GetTestResultsUri()
            => $"{_options.CollectionUri}{_options.TeamProject}/_build/results?buildId={_options.BuildId}&view=ms.vss-test-web.build-test-results-tab";

        private static string GetConsoleOutputText(string consoleOutputUri)
            => string.IsNullOrEmpty(consoleOutputUri) ? "no console link available" : consoleOutputUri;

        private async Task<EntryResubmissionResult> ResubmitFailedJobsAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🔁 Checking for failed Helix jobs to resubmit the failed work items...");

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
            return string.IsNullOrEmpty(job.StageName)
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

        private sealed record FailedWorkItemConsoleInfo(
            string JobName,
            string WorkItemName,
            string State,
            string ConsoleOutput);

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
                "Helix Job Monitor timed out after {TimeoutMinutes} minute(s) ({Timeout}). {UnfinishedCount} Helix job(s) had not finished: {UnfinishedJobs}" + Environment.NewLine,
                timeout.TotalMinutes,
                timeout,
                unfinishedJobs.Count,
                string.Join(Environment.NewLine + "- ", unfinishedJobs.Select(j => j.DetailsUri)));
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
