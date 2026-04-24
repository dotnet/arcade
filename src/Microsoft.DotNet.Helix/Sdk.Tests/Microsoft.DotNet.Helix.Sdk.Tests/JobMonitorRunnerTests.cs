// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.JobMonitor;
using Microsoft.DotNet.Helix.JobMonitor.Models;
using Microsoft.DotNet.Helix.Sdk.Tests.Fakes;
using Microsoft.DotNet.Helix.Sdk.Tests.ScenarioHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Microsoft.DotNet.Helix.Sdk.Tests.ScenarioHelpers.ScenarioHelpers;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    [Collection("NonParallel")]
    public class JobMonitorRunnerTests
    {
        // -----------------------------------------------------------------------
        // Happy Path
        // -----------------------------------------------------------------------

        [Fact]
        public async Task AllJobsPassOnFirstPoll_ExitZero_OneTestRunPerJob()
        {
            var (azdo, helix, runner, delayCount) = CreateScenario(
                timelineSnapshots: [[PipelineJob("Build Linux", "completed", "succeeded"), MonitorJob()]],
                helixSnapshots: [(jobs: [HelixJob("job-linux", "finished")], passFail: Dict(("job-linux", PassFail(passed: ["wi-1"]))))]);

            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Equal(0, delayCount());
            Assert.Single(azdo.CreatedTestRuns);
            Assert.Single(azdo.CompletedTestRunIds);
            Assert.Equal(["job-linux"], azdo.UploadedJobNames);
        }

        [Fact]
        public async Task MultipleJobsAcrossMultiplePolls_ProcessesEachOnce()
        {
            var (azdo, helix, runner, delayCount) = CreateScenario(
                timelineSnapshots:
                [
                    [PipelineJob("Build Linux", "inProgress"), PipelineJob("Build Win", "inProgress"), MonitorJob()],
                    [PipelineJob("Build Linux", "completed", "succeeded"), PipelineJob("Build Win", "completed", "succeeded"), MonitorJob()],
                    [PipelineJob("Build Linux", "completed", "succeeded"), PipelineJob("Build Win", "completed", "succeeded"), MonitorJob()],
                ],
                helixSnapshots:
                [
                    (jobs: [HelixJob("job-linux", "running")], passFail: EmptyPassFail()),
                    (jobs: [HelixJob("job-linux", "finished"), HelixJob("job-win", "running")], passFail: Dict(("job-linux", PassFail(passed: ["linux-wi"])))),
                    (jobs: [HelixJob("job-linux", "finished"), HelixJob("job-win", "finished")], passFail: Dict(("job-linux", PassFail(passed: ["linux-wi"])), ("job-win", PassFail(passed: ["win-wi"])))),
                ]);

            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Equal(2, delayCount());
            Assert.Equal(2, azdo.CreatedTestRuns.Count);
            Assert.Equal(2, azdo.CompletedTestRunIds.Count);
            Assert.Equal(["job-linux", "job-win"], azdo.UploadedJobNames);
        }

        [Fact]
        public async Task StageCompletesWithNoHelixJobs_ExitZero_NoTestRuns()
        {
            var (azdo, helix, runner, _) = CreateScenario(
                timelineSnapshots: [[PipelineJob("Build Linux", "completed", "succeeded"), MonitorJob()]],
                helixSnapshots: [(jobs: Array.Empty<HelixJobInfo>(), passFail: EmptyPassFail())]);

            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Empty(azdo.CreatedTestRuns);
            Assert.Empty(azdo.UploadedJobNames);
        }

        // -----------------------------------------------------------------------
        // Failure Scenarios
        // -----------------------------------------------------------------------

        [Fact]
        public async Task PipelineJobFailsBeforeHelixSubmission_ExitOne_NoTestRuns()
        {
            var (azdo, _, runner, _) = CreateScenario(
                timelineSnapshots: [[PipelineJob("Build Linux", "completed", "failed"), MonitorJob()]],
                helixSnapshots: [(jobs: Array.Empty<HelixJobInfo>(), passFail: EmptyPassFail())]);

            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(1, exitCode);
            Assert.Empty(azdo.CreatedTestRuns);
        }

        [Fact]
        public async Task PipelineJobCanceled_ExitOne()
        {
            var (_, _, runner, _) = CreateScenario(
                timelineSnapshots: [[PipelineJob("Build Linux", "completed", "canceled"), MonitorJob()]],
                helixSnapshots: [(jobs: Array.Empty<HelixJobInfo>(), passFail: EmptyPassFail())]);

            Assert.Equal(1, await runner.RunAsync(CancellationToken.None));
        }

        [Fact]
        public async Task HelixJobFails_ExitOne_ResultsStillUploaded()
        {
            var (azdo, _, runner, _) = CreateScenario(
                timelineSnapshots: [[PipelineJob("Build Linux", "completed", "succeeded"), MonitorJob()]],
                helixSnapshots: [(jobs: [HelixJob("job-linux", "finished")], passFail: Dict(("job-linux", PassFail(failed: ["wi-1"]))))]);

            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(1, exitCode);
            Assert.Equal(["job-linux"], azdo.UploadedJobNames);
            Assert.Single(azdo.CompletedTestRunIds);
        }

        [Fact]
        public async Task AllHelixWorkItemsFail_ExitOne_ResultsUploaded()
        {
            var (azdo, _, runner, _) = CreateScenario(
                timelineSnapshots: [[PipelineJob("Build Linux", "completed", "succeeded"), MonitorJob()]],
                helixSnapshots: [(jobs: [HelixJob("job-linux", "finished")], passFail: Dict(("job-linux", PassFail(failed: ["wi-1", "wi-2"]))))]);

            Assert.Equal(1, await runner.RunAsync(CancellationToken.None));
            Assert.Equal(["job-linux"], azdo.UploadedJobNames);
        }

        [Fact]
        public async Task InfrastructureFailure_HelixJobFailedNoWorkItems_ExitOne()
        {
            var (azdo, _, runner, _) = CreateScenario(
                timelineSnapshots: [[PipelineJob("Build Linux", "completed", "succeeded"), MonitorJob()]],
                helixSnapshots: [(jobs: [HelixJob("job-linux", "failed")], passFail: Dict(("job-linux", PassFail())))]);

            Assert.Equal(1, await runner.RunAsync(CancellationToken.None));
            Assert.Equal(["job-linux"], azdo.UploadedJobNames);
        }

        [Fact]
        public async Task MultipleHelixJobsAllFail_ExitOne_AllResultsUploaded()
        {
            var (azdo, _, runner, _) = CreateScenario(
                timelineSnapshots: [[PipelineJob("Build Linux", "completed", "succeeded"), PipelineJob("Build Win", "completed", "succeeded"), MonitorJob()]],
                helixSnapshots: [(jobs: [HelixJob("job-linux", "finished"), HelixJob("job-win", "finished")], passFail: Dict(("job-linux", PassFail(failed: ["linux-wi"])), ("job-win", PassFail(failed: ["win-wi"]))))]);

            Assert.Equal(1, await runner.RunAsync(CancellationToken.None));
            Assert.Equal(2, azdo.UploadedJobNames.Count);
        }

        [Fact]
        public async Task PipelineFailsButHelixResultsStillUploaded()
        {
            var (azdo, _, runner, _) = CreateScenario(
                timelineSnapshots: [[PipelineJob("Build Linux", "completed", "failed"), MonitorJob()]],
                helixSnapshots: [(jobs: [HelixJob("job-linux", "finished")], passFail: Dict(("job-linux", PassFail(passed: ["wi-1"]))))]);

            Assert.Equal(1, await runner.RunAsync(CancellationToken.None));
            Assert.Equal(["job-linux"], azdo.UploadedJobNames);
        }

        // -----------------------------------------------------------------------
        // Retry / Rerun Scenarios
        // -----------------------------------------------------------------------

        [Fact]
        public async Task MonitorRetry_SkipsPreviouslyProcessed()
        {
            var azdo = new FakeAzureDevOpsService().WithPreviouslyProcessedJob("job-linux");
            var helix = new FakeHelixService();
            ConfigureSnapshots(azdo, helix,
                [[PipelineJob("Build Linux", "completed", "succeeded"), PipelineJob("Build Win", "completed", "succeeded"), MonitorJob()]],
                [(jobs: [HelixJob("job-linux", "finished"), HelixJob("job-win", "finished")], passFail: Dict(("job-linux", PassFail(passed: ["linux-wi"])), ("job-win", PassFail(passed: ["win-wi"]))))]);
            var runner = CreateRunner(azdo, helix);

            Assert.Equal(0, await runner.RunAsync(CancellationToken.None));
            Assert.Single(azdo.CreatedTestRuns);
            Assert.Equal(["job-win"], azdo.UploadedJobNames);
        }

        [Fact]
        public async Task MonitorRetry_ProcessesReplacementDelta()
        {
            var azdo = new FakeAzureDevOpsService().WithPreviouslyProcessedJob("job-linux-attempt1");
            var helix = new FakeHelixService();
            ConfigureSnapshots(azdo, helix,
                [[PipelineJob("Build Linux (retry)", "completed", "succeeded"), MonitorJob()]],
                [(jobs: [HelixJob("job-linux-attempt1", "failed"), HelixJob("job-linux-attempt2", "finished")], passFail: Dict(("job-linux-attempt1", PassFail(failed: ["wi-2"])), ("job-linux-attempt2", PassFail(passed: ["wi-2"]))))]);
            var runner = CreateRunner(azdo, helix);

            Assert.Equal(0, await runner.RunAsync(CancellationToken.None));
            Assert.Equal(["job-linux-attempt2"], azdo.UploadedJobNames);
        }

        [Fact]
        public async Task StageRerun_NewJobsQueuedAlongsideOld_WaitsForNew()
        {
            var azdo = new FakeAzureDevOpsService().WithPreviouslyProcessedJob("job-linux-v1").WithPreviouslyProcessedJob("job-win-v1");
            var helix = new FakeHelixService();
            ConfigureSnapshots(azdo, helix,
                [
                    [PipelineJob("Build Linux", "completed", "succeeded"), PipelineJob("Build Win", "inProgress"), MonitorJob()],
                    [PipelineJob("Build Linux", "completed", "succeeded"), PipelineJob("Build Win", "completed", "succeeded"), MonitorJob()],
                ],
                [
                    (jobs: [HelixJob("job-linux-v1", "finished"), HelixJob("job-linux-v2", "running")], passFail: EmptyPassFail()),
                    (jobs: [HelixJob("job-linux-v1", "finished"), HelixJob("job-linux-v2", "finished"), HelixJob("job-win-v2", "finished")], passFail: Dict(("job-linux-v2", PassFail(passed: ["linux-wi"])), ("job-win-v2", PassFail(passed: ["win-wi"])))),
                ]);
            int delayCount = 0;
            var runner = new JobMonitorRunner(DefaultOptions(), NullLogger.Instance, azdo, helix, (_, ct) => { delayCount++; AdvanceFakes(azdo, helix); return Task.CompletedTask; });

            Assert.Equal(0, await runner.RunAsync(CancellationToken.None));
            Assert.Equal(1, delayCount);
            Assert.Equal(["job-linux-v2", "job-win-v2"], azdo.UploadedJobNames);
        }

        [Fact]
        public async Task MultipleRetries_SkipsAllPriorGenerations()
        {
            var azdo = new FakeAzureDevOpsService()
                .WithPreviouslyProcessedJob("job-linux-attempt1").WithPreviouslyProcessedJob("job-linux-attempt2")
                .WithPreviouslyProcessedJob("job-win-attempt1").WithPreviouslyProcessedJob("job-win-attempt2");
            var helix = new FakeHelixService();
            ConfigureSnapshots(azdo, helix,
                [[PipelineJob("Build Linux", "completed", "succeeded"), PipelineJob("Build Win", "completed", "succeeded"), MonitorJob()]],
                [(jobs: [HelixJob("job-linux-attempt1", "finished"), HelixJob("job-linux-attempt2", "finished"), HelixJob("job-linux-attempt3", "finished"), HelixJob("job-win-attempt1", "finished"), HelixJob("job-win-attempt2", "finished"), HelixJob("job-win-attempt3", "finished")], passFail: Dict(("job-linux-attempt3", PassFail(passed: ["linux-wi"])), ("job-win-attempt3", PassFail(passed: ["win-wi"]))))]);
            var runner = CreateRunner(azdo, helix);

            Assert.Equal(0, await runner.RunAsync(CancellationToken.None));
            Assert.Equal(["job-linux-attempt3", "job-win-attempt3"], azdo.UploadedJobNames);
        }

        [Fact]
        public async Task MonitorCrashAndRestart_ProcessesRemainingDelta()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();
            ConfigureSnapshots(azdo, helix,
                [[PipelineJob("Build Linux", "completed", "succeeded"), PipelineJob("Build Win", "completed", "succeeded"), MonitorJob()]],
                [(jobs: [HelixJob("job-linux", "finished"), HelixJob("job-win", "finished")], passFail: Dict(("job-linux", PassFail(passed: ["linux-wi"])), ("job-win", PassFail(passed: ["win-wi"]))))]);

            helix.FailDownloadForJob("job-win");
            var runner1 = CreateRunner(azdo, helix);
            Assert.Equal(1, await runner1.RunAsync(CancellationToken.None));
            Assert.Equal(["job-linux"], azdo.UploadedJobNames);

            helix.ClearDownloadFailures();
            var runner2 = CreateRunner(azdo, helix);
            Assert.Equal(0, await runner2.RunAsync(CancellationToken.None));
            Assert.Equal(["job-linux", "job-win"], azdo.UploadedJobNames);
        }

        [Fact]
        public async Task RetryWithFailedSubsetResubmitted_OnlyNewJobProcessed()
        {
            var azdo = new FakeAzureDevOpsService().WithPreviouslyProcessedJob("job-linux-attempt1");
            var helix = new FakeHelixService();
            ConfigureSnapshots(azdo, helix,
                [[PipelineJob("Build Linux (retry)", "completed", "succeeded"), MonitorJob()]],
                [(jobs: [HelixJob("job-linux-attempt1", "failed"), HelixJob("job-linux-attempt2", "finished")], passFail: Dict(("job-linux-attempt1", PassFail(failed: ["wi-2"])), ("job-linux-attempt2", PassFail(passed: ["wi-2"]))))]);
            var runner = CreateRunner(azdo, helix);

            Assert.Equal(0, await runner.RunAsync(CancellationToken.None));
            Assert.Equal(["job-linux-attempt2"], azdo.UploadedJobNames);
        }

        // -----------------------------------------------------------------------
        // Edge Cases
        // -----------------------------------------------------------------------

        [Fact]
        public async Task MonitorTimesOut_ThrowsOperationCanceledException()
        {
            var (_, _, runner, _) = CreateScenario(
                timelineSnapshots: [[PipelineJob("Build Linux", "inProgress"), MonitorJob()]],
                helixSnapshots: [(jobs: [HelixJob("job-linux", "running")], passFail: EmptyPassFail())]);

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAsync<OperationCanceledException>(() => runner.RunAsync(cts.Token));
        }

        [Fact]
        public async Task AllPipelineJobsFailWhileHelixStillRunning_ExitsImmediately()
        {
            var (azdo, _, runner, delayCount) = CreateScenario(
                timelineSnapshots: [[PipelineJob("Build Linux", "completed", "failed"), MonitorJob()]],
                helixSnapshots: [(jobs: [HelixJob("job-linux", "running")], passFail: EmptyPassFail())]);

            Assert.Equal(1, await runner.RunAsync(CancellationToken.None));
            Assert.Equal(0, delayCount());
        }

        [Fact]
        public async Task DownloadFailureMidStream_RetryReusesTestRun_NoDuplicate()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();
            ConfigureSnapshots(azdo, helix,
                [[PipelineJob("Build Linux", "completed", "succeeded"), PipelineJob("Build Win", "completed", "succeeded"), MonitorJob()]],
                [(jobs: [HelixJob("job-linux", "finished"), HelixJob("job-win", "finished")], passFail: Dict(("job-linux", PassFail(passed: ["linux-wi"])), ("job-win", PassFail(passed: ["win-wi"]))))]);

            helix.FailDownloadForJob("job-win");
            var runner1 = CreateRunner(azdo, helix);
            Assert.Equal(1, await runner1.RunAsync(CancellationToken.None));
            Assert.Equal(["job-linux"], azdo.UploadedJobNames);

            helix.ClearDownloadFailures();
            var runner2 = CreateRunner(azdo, helix);
            Assert.Equal(0, await runner2.RunAsync(CancellationToken.None));
            Assert.Equal(["job-linux", "job-win"], azdo.UploadedJobNames);

            // Key invariant: exactly 1 test run CREATED for job-win (second call reused in-progress one)
            Assert.Equal(1, azdo.CreatedTestRuns.Count(n => n.Equals("job-win", StringComparison.OrdinalIgnoreCase)));
        }

        [Fact]
        public async Task AllPipelineJobsCanceled_ExitOne_NoUploads()
        {
            var (azdo, _, runner, _) = CreateScenario(
                timelineSnapshots: [[PipelineJob("Build Linux", "completed", "canceled"), PipelineJob("Build Win", "completed", "canceled"), MonitorJob()]],
                helixSnapshots: [(jobs: Array.Empty<HelixJobInfo>(), passFail: EmptyPassFail())]);

            Assert.Equal(1, await runner.RunAsync(CancellationToken.None));
            Assert.Empty(azdo.UploadedJobNames);
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static JobMonitorOptions DefaultOptions() => new()
        {
            BuildId = "123",
            CollectionUri = "https://dev.azure.com/dnceng/",
            JobMonitorName = DefaultMonitorName,
            MaximumWaitMinutes = 1,
            PollingIntervalSeconds = 0,
            Organization = "dotnet",
            RepositoryName = "arcade",
            PrNumber = 99999,
            SystemAccessToken = "token",
            TeamProject = "public",
            WorkingDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "job-monitor-test"),
        };

        private static readonly Func<TimeSpan, CancellationToken, Task> NoDelay = (_, _) => Task.CompletedTask;
        private static Dictionary<string, HelixJobPassFail> EmptyPassFail() => new(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, HelixJobPassFail> Dict(params (string jobName, HelixJobPassFail pf)[] entries)
        {
            var dict = new Dictionary<string, HelixJobPassFail>(StringComparer.OrdinalIgnoreCase);
            foreach (var (jobName, pf) in entries) dict[jobName] = pf;
            return dict;
        }

        private static void AdvanceFakes(FakeAzureDevOpsService azdo, FakeHelixService helix) { azdo.AdvanceTimeline(); helix.AdvanceSnapshot(); }

        private static void ConfigureSnapshots(
            FakeAzureDevOpsService azdo, FakeHelixService helix,
            AzureDevOpsTimelineRecord[][] timelineSnapshots,
            (HelixJobInfo[] jobs, Dictionary<string, HelixJobPassFail> passFail)[] helixSnapshots)
        {
            foreach (var timeline in timelineSnapshots) azdo.AddTimelineSnapshot(timeline);
            foreach (var (jobs, passFail) in helixSnapshots) helix.AddSnapshot(jobs, passFail);
        }

        private static JobMonitorRunner CreateRunner(FakeAzureDevOpsService azdo, FakeHelixService helix, Func<TimeSpan, CancellationToken, Task> delayFunc = null)
            => new(DefaultOptions(), NullLogger.Instance, azdo, helix, delayFunc ?? NoDelay);

        private static (FakeAzureDevOpsService azdo, FakeHelixService helix, JobMonitorRunner runner, Func<int> delayCount) CreateScenario(
            AzureDevOpsTimelineRecord[][] timelineSnapshots,
            (HelixJobInfo[] jobs, Dictionary<string, HelixJobPassFail> passFail)[] helixSnapshots)
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();
            ConfigureSnapshots(azdo, helix, timelineSnapshots, helixSnapshots);
            int delays = 0;
            var runner = new JobMonitorRunner(DefaultOptions(), NullLogger.Instance, azdo, helix, (_, ct) => { delays++; AdvanceFakes(azdo, helix); return Task.CompletedTask; });
            return (azdo, helix, runner, () => delays);
        }
    }
}
