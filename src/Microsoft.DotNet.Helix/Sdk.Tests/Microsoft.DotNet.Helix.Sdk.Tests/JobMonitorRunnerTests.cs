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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Microsoft.DotNet.Helix.Sdk.Tests.ScenarioHelpers.ScenarioHelpers;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    [Collection("NonParallel")]
    public class JobMonitorRunnerTests
    {
        /// <summary>
        /// Single pipeline job goes from queued → in progress → completed (succeeded).
        /// No Helix jobs are ever submitted.
        /// The monitor should poll 3 times and exit with code 0.
        /// </summary>
        [Fact]
        public async Task SinglePipelineJobSucceeds_NoHelixJobs_ExitZero()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            // Poll 1: other job is queued
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "pending"));

            // Poll 2: other job is in progress
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "inProgress"));

            // Poll 3: other job completed successfully
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "completed", "succeeded"));

            // No Helix jobs on any poll
            helix.AddResponse(jobs: []);
            helix.AddResponse(jobs: []);
            helix.AddResponse(jobs: []);

            var runner = CreateRunner(azdo, helix);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Equal(3, azdo.TimelineCallCount);
            Assert.Empty(azdo.CreatedTestRuns);
            Assert.Empty(azdo.UploadedJobNames);
        }

        /// <summary>
        /// Two pipeline jobs (plus the monitor). No Helix jobs submitted.
        /// Jobs finish at different times: one passes, one fails.
        /// The monitor should detect the failure and exit with code 1.
        /// </summary>
        [Fact]
        public async Task TwoPipelineJobs_OnePassesOneFails_NoHelixJobs_ExitOne()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            // Poll 1: both jobs queued
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "pending"),
                PipelineJob("Build Windows", "pending"));

            // Poll 2: Linux in progress, Windows still queued
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "inProgress"),
                PipelineJob("Build Windows", "pending"));

            // Poll 3: Linux completed (passed), Windows in progress
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "completed", "succeeded"),
                PipelineJob("Build Windows", "inProgress"));

            // Poll 4: Linux completed (passed), Windows completed (failed)
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "completed", "succeeded"),
                PipelineJob("Build Windows", "completed", "failed"));

            // No Helix jobs on any poll
            helix.AddResponse(jobs: []);
            helix.AddResponse(jobs: []);
            helix.AddResponse(jobs: []);
            helix.AddResponse(jobs: []);

            var runner = CreateRunner(azdo, helix);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(1, exitCode);
            Assert.Equal(4, azdo.TimelineCallCount);
            Assert.Empty(azdo.CreatedTestRuns);
            Assert.Empty(azdo.UploadedJobNames);
        }

        /// <summary>
        /// Two pipeline jobs (plus the monitor). No Helix jobs submitted.
        /// Both pipeline jobs fail.
        /// The monitor should exit with code 1.
        /// </summary>
        [Fact]
        public async Task TwoPipelineJobs_AllFailed_NoHelixJobs_ExitOne()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            // Poll 1: both in progress
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "inProgress"),
                PipelineJob("Build Windows", "inProgress"));

            // Poll 2: Linux failed, Windows still going
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "completed", "failed"),
                PipelineJob("Build Windows", "inProgress"));

            // Poll 3: both failed
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "completed", "failed"),
                PipelineJob("Build Windows", "completed", "failed"));

            helix.AddResponse(jobs: []);
            helix.AddResponse(jobs: []);
            helix.AddResponse(jobs: []);

            var runner = CreateRunner(azdo, helix);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(1, exitCode);
            Assert.Equal(3, azdo.TimelineCallCount);
            Assert.Empty(azdo.CreatedTestRuns);
            Assert.Empty(azdo.UploadedJobNames);
        }

        /// <summary>
        /// Two pipeline jobs (plus the monitor). No Helix jobs submitted.
        /// Both pipeline jobs pass.
        /// The monitor should exit with code 0.
        /// </summary>
        [Fact]
        public async Task TwoPipelineJobs_AllPassed_NoHelixJobs_ExitZero()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            // Poll 1: both in progress
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "inProgress"),
                PipelineJob("Build Windows", "inProgress"));

            // Poll 2: Linux done, Windows still going
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "completed", "succeeded"),
                PipelineJob("Build Windows", "inProgress"));

            // Poll 3: both done
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "completed", "succeeded"),
                PipelineJob("Build Windows", "completed", "succeeded"));

            helix.AddResponse(jobs: []);
            helix.AddResponse(jobs: []);
            helix.AddResponse(jobs: []);

            var runner = CreateRunner(azdo, helix);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Equal(3, azdo.TimelineCallCount);
            Assert.Empty(azdo.CreatedTestRuns);
            Assert.Empty(azdo.UploadedJobNames);
        }

        /// <summary>
        /// Three jobs (monitor + Build Linux + Build Windows). No Helix jobs.
        /// First run: Linux passes, Windows fails → monitor exits 1.
        /// Then a retry happens: the monitor and Build Windows are re-run (attempt 2).
        /// In AzDO, retried jobs appear in the timeline with attempt=2. The old
        /// attempt=1 records for the retried jobs are replaced. Non-retried jobs
        /// (Build Linux) keep their attempt=1 records.
        /// The retried Windows job queues, runs, then passes.
        /// The monitor should exit 0 on the retry.
        /// </summary>
        [Fact]
        public async Task RetryAfterFailure_RetriedJobPasses_ExitZero()
        {
            // --- First run (attempt 1) ---
            var azdo1 = new FakeAzureDevOpsService();
            var helix1 = new FakeHelixService();

            // Poll 1: both jobs queued
            azdo1.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "pending"),
                PipelineJob("Build Windows", "pending"));

            // Poll 2: both in progress
            azdo1.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "inProgress"),
                PipelineJob("Build Windows", "inProgress"));

            // Poll 3: Linux passed, Windows still going
            azdo1.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "completed", "succeeded"),
                PipelineJob("Build Windows", "inProgress"));

            // Poll 4: Linux passed, Windows failed
            azdo1.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Build Linux", "completed", "succeeded"),
                PipelineJob("Build Windows", "completed", "failed"));

            helix1.AddResponse(jobs: []);
            helix1.AddResponse(jobs: []);
            helix1.AddResponse(jobs: []);
            helix1.AddResponse(jobs: []);

            var runner1 = CreateRunner(azdo1, helix1);
            int exitCode1 = await runner1.RunAsync(CancellationToken.None);

            Assert.Equal(1, exitCode1);
            Assert.Equal(4, azdo1.TimelineCallCount);

            // --- Retry (attempt 2): monitor and Build Windows re-run ---
            // AzDO replaces the retried jobs' records with attempt=2.
            // Build Linux was not retried, so it keeps attempt=1 and its completed state.
            var azdo2 = new FakeAzureDevOpsService();
            var helix2 = new FakeHelixService();

            // Poll 1: Linux still passed (attempt 1), Windows queued (attempt 2)
            azdo2.AddTimelineResponse(
                MonitorJob(attempt: 2, previousAttempts: [PreviousAttempt(1)]),
                PipelineJob("Build Linux", "completed", "succeeded", attempt: 1),
                PipelineJob("Build Windows", "pending", attempt: 2,
                    previousAttempts: [PreviousAttempt(1)]));

            // Poll 2: Windows in progress
            azdo2.AddTimelineResponse(
                MonitorJob(attempt: 2, previousAttempts: [PreviousAttempt(1)]),
                PipelineJob("Build Linux", "completed", "succeeded", attempt: 1),
                PipelineJob("Build Windows", "inProgress", attempt: 2,
                    previousAttempts: [PreviousAttempt(1)]));

            // Poll 3: Windows completed (passed this time)
            azdo2.AddTimelineResponse(
                MonitorJob(attempt: 2, previousAttempts: [PreviousAttempt(1)]),
                PipelineJob("Build Linux", "completed", "succeeded", attempt: 1),
                PipelineJob("Build Windows", "completed", "succeeded", attempt: 2,
                    previousAttempts: [PreviousAttempt(1)]));

            helix2.AddResponse(jobs: []);
            helix2.AddResponse(jobs: []);
            helix2.AddResponse(jobs: []);

            var runner2 = CreateRunner(azdo2, helix2);
            int exitCode2 = await runner2.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode2);
            Assert.Equal(3, azdo2.TimelineCallCount);
            Assert.Empty(azdo2.CreatedTestRuns);
            Assert.Empty(azdo2.UploadedJobNames);
        }

        /// <summary>
        /// Two jobs: monitor + a build job that submits Helix work.
        /// The build job queues, runs for a couple iterations, then completes.
        /// A single Helix job appears (running), then its work items finish (passed).
        /// The monitor should upload test results and exit 0.
        /// </summary>
        [Fact]
        public async Task BuildJobSubmitsHelixWork_WorkItemsPassed_ResultsUploaded_ExitZero()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            // --- AzDO timeline ---
            // Poll 1: build job queued
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "pending"));

            // Poll 2: build job in progress
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "inProgress"));

            // Poll 3: build job completed (it has submitted Helix work and exited)
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"));

            // Polls 4-6: build job still completed (monitor waiting for Helix)
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"));
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"));
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"));

            // --- Helix ---
            // Poll 1: no Helix jobs yet (build hasn't submitted)
            helix.AddResponse(jobs: []);

            // Poll 2: no Helix jobs yet (build still running)
            helix.AddResponse(jobs: []);

            // Poll 3: Helix job appears, still running (submitted by the build job)
            helix.AddResponse(jobs: [HelixJob("helix-linux-tests", "running")]);

            // Poll 4: Helix job still running (work items executing)
            helix.AddResponse(jobs: [HelixJob("helix-linux-tests", "running")]);

            // Poll 5: Helix job still running (work items still going)
            helix.AddResponse(jobs: [HelixJob("helix-linux-tests", "running")]);

            // Poll 6: Helix job finished — work items passed
            helix.AddResponse(
                jobs: [HelixJob("helix-linux-tests", "finished")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux-tests"] = PassFail(passed: ["workitem-1"]),
                });

            var runner = CreateRunner(azdo, helix);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            // Monitor should exit successfully
            Assert.Equal(0, exitCode);

            // 6 poll iterations (5 delays before exit on 6th)
            Assert.Equal(6, azdo.TimelineCallCount);

            // One test run created and completed for the Helix job
            Assert.Single(azdo.CreatedTestRuns);
            Assert.Single(azdo.CompletedTestRunIds);

            // Test results uploaded for the Helix job
            Assert.Equal(["helix-linux-tests"], azdo.UploadedJobNames);
        }

        /// <summary>
        /// Test result publishing is separate from pass/fail calculation. If the Helix work item
        /// passed but its test result files fail to upload, the monitor still exits 0.
        /// </summary>
        [Fact]
        public async Task PassedHelixWork_UploadFails_ExitZero()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"));

            helix.AddResponse(
                jobs: [HelixJob("helix-linux", "finished")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["workitem-1"]),
                });
            helix.FailDownloadForJob("helix-linux");

            var runner = CreateRunner(azdo, helix);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Single(azdo.CreatedTestRuns);
            Assert.Single(azdo.CompletedTestRunIds);
            Assert.Empty(azdo.UploadedJobNames);
        }

        /// <summary>
        /// A single build job submits a Helix job with multiple work items.
        /// The work items finish at different times (the Helix job goes from running → finished
        /// only when the last work item completes). All work items pass.
        /// </summary>
        [Fact]
        public async Task MultipleWorkItems_FinishAtDifferentTimes_AllPass_ExitZero()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            // Build job runs then completes quickly
            azdo.AddTimelineResponse(MonitorJob(), PipelineJob("Test Linux", "inProgress"));
            azdo.AddTimelineResponse(MonitorJob(), PipelineJob("Test Linux", "completed", "succeeded"));
            // Remaining polls: build done, waiting for Helix
            azdo.AddTimelineResponse(MonitorJob(), PipelineJob("Test Linux", "completed", "succeeded"));
            azdo.AddTimelineResponse(MonitorJob(), PipelineJob("Test Linux", "completed", "succeeded"));

            // Poll 1: no Helix jobs yet
            helix.AddResponse(jobs: []);
            // Poll 2: Helix job appears, running (work items not all done)
            helix.AddResponse(jobs: [HelixJob("helix-linux", "running")]);
            // Poll 3: still running (some work items done, some not)
            helix.AddResponse(jobs: [HelixJob("helix-linux", "running")]);
            // Poll 4: finished — all 3 work items passed
            helix.AddResponse(
                jobs: [HelixJob("helix-linux", "finished")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["wi-fast", "wi-medium", "wi-slow"]),
                });

            var runner = CreateRunner(azdo, helix);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Equal(4, azdo.TimelineCallCount);
            Assert.Single(azdo.CreatedTestRuns);
            Assert.Single(azdo.CompletedTestRunIds);
            Assert.Equal(["helix-linux"], azdo.UploadedJobNames);
        }

        /// <summary>
        /// Two build jobs each submit Helix work. Work items finish at different times.
        /// Some work items pass, some fail.
        /// The monitor should upload all results and exit 1 (due to failures).
        /// </summary>
        [Fact]
        public async Task TwoJobsSubmitHelixWork_MixedPassFail_ExitOne()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            // Both build jobs run and complete
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "inProgress"),
                PipelineJob("Test Windows", "inProgress"));
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"),
                PipelineJob("Test Windows", "inProgress"));
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"),
                PipelineJob("Test Windows", "completed", "succeeded"));
            // Remaining polls while Helix runs
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"),
                PipelineJob("Test Windows", "completed", "succeeded"));
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"),
                PipelineJob("Test Windows", "completed", "succeeded"));

            // Poll 1: no Helix jobs
            helix.AddResponse(jobs: []);
            // Poll 2: Linux helix job appears (running)
            helix.AddResponse(jobs: [HelixJob("helix-linux", "running")]);
            // Poll 3: Linux finished (has failures), Windows helix job appears (running)
            helix.AddResponse(
                jobs: [HelixJob("helix-linux", "finished"), HelixJob("helix-windows", "running")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["linux-wi-1"], failed: ["linux-wi-2"]),
                });
            // Poll 4: Windows still running
            helix.AddResponse(
                jobs: [HelixJob("helix-linux", "finished"), HelixJob("helix-windows", "running")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["linux-wi-1"], failed: ["linux-wi-2"]),
                });
            // Poll 5: Windows finished (all passed)
            helix.AddResponse(
                jobs: [HelixJob("helix-linux", "finished"), HelixJob("helix-windows", "finished")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["linux-wi-1"], failed: ["linux-wi-2"]),
                    ["helix-windows"] = PassFail(passed: ["win-wi-1", "win-wi-2"]),
                });

            var runner = CreateRunner(azdo, helix);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            // Exit 1 because helix-linux had a failed work item
            Assert.Equal(1, exitCode);
            Assert.Equal(5, azdo.TimelineCallCount);
            // Both Helix jobs had results uploaded
            Assert.Equal(2, azdo.CreatedTestRuns.Count);
            Assert.Equal(2, azdo.CompletedTestRunIds.Count);
            Assert.Contains("helix-linux", azdo.UploadedJobNames);
            Assert.Contains("helix-windows", azdo.UploadedJobNames);
        }

        /// <summary>
        /// Stage-scoped monitoring. A job in another stage runs — the monitor should
        /// ignore it entirely. Only the job in the monitor's own stage matters.
        /// </summary>
        [Fact]
        public async Task StageScopedMonitor_IgnoresJobsOutsideStage_ExitZero()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            // Timeline includes jobs from two stages: "Test" (monitor's stage) and "Build".
            // The Build stage job is still running, but the monitor shouldn't care.
            azdo.AddTimelineResponse(
                StageRecord("Test", "stage-test", "inProgress"),
                MonitorJob(parentId: "stage-test"),
                PipelineJob("Test Linux", "inProgress", parentId: "stage-test"),
                StageRecord("Build", "stage-build", "inProgress"),
                PipelineJob("Build Windows", "inProgress", parentId: "stage-build"));

            azdo.AddTimelineResponse(
                StageRecord("Test", "stage-test", "inProgress"),
                MonitorJob(parentId: "stage-test"),
                PipelineJob("Test Linux", "completed", "succeeded", parentId: "stage-test"),
                StageRecord("Build", "stage-build", "inProgress"),
                PipelineJob("Build Windows", "inProgress", parentId: "stage-build"));

            // No Helix jobs
            helix.AddResponse(jobs: []);
            helix.AddResponse(jobs: []);

            var runner = CreateRunner(azdo, helix, stageName: "Test");
            int exitCode = await runner.RunAsync(CancellationToken.None);

            // Monitor only watches Test stage — Test Linux passed, no Helix → exit 0
            // Build Windows being in progress doesn't block the monitor.
            Assert.Equal(0, exitCode);
            Assert.Equal(2, azdo.TimelineCallCount);
            Assert.Empty(azdo.UploadedJobNames);
        }

        /// <summary>
        /// Stage-scoped monitoring. A job outside the monitor's stage submits Helix work.
        /// The monitor should ignore that Helix job entirely.
        /// </summary>
        [Fact]
        public async Task StageScopedMonitor_IgnoresHelixJobsFromOtherStage_ExitZero()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            // Test stage job completes quickly with no Helix submissions.
            // Build stage job submits a Helix job, but that's not the monitor's concern.
            azdo.AddTimelineResponse(
                StageRecord("Test", "stage-test", "inProgress"),
                MonitorJob(parentId: "stage-test"),
                PipelineJob("Test Linux", "completed", "succeeded", parentId: "stage-test"),
                StageRecord("Build", "stage-build", "inProgress"),
                PipelineJob("Build Windows", "inProgress", parentId: "stage-build"));

            // Helix returns a job from the Build stage — monitor should ignore it
            helix.AddResponse(
                jobs: [HelixJob("helix-build-windows", "running", stageName: "Build")]);

            var runner = CreateRunner(azdo, helix, stageName: "Test");
            int exitCode = await runner.RunAsync(CancellationToken.None);

            // Test stage is done, no Helix jobs in Test stage → exit 0
            Assert.Equal(0, exitCode);
            Assert.Equal(1, azdo.TimelineCallCount);
            Assert.Empty(azdo.UploadedJobNames);
            Assert.Empty(azdo.CreatedTestRuns);
        }

        /// <summary>
        /// Stage-scoped monitoring also applies to entry-time retry. A failed Helix job from
        /// another stage must not be resubmitted or uploaded by this monitor.
        /// </summary>
        [Fact]
        public async Task StageScopedMonitor_DoesNotResubmitFailedHelixJobsOutsideStage_ExitZero()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            azdo.AddTimelineResponse(
                StageRecord("Test", "stage-test", "inProgress"),
                MonitorJob(parentId: "stage-test"),
                PipelineJob("Test Linux", "completed", "succeeded", parentId: "stage-test"),
                StageRecord("Build", "stage-build", "completed", "failed"),
                PipelineJob("Build Windows", "completed", "failed", parentId: "stage-build"));

            helix.AddResponse(
                jobs: [HelixJob("helix-build-windows", "finished", stageName: "Build")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-build-windows"] = PassFail(failed: ["build-fail"]),
                });
            helix.ConfigureResubmission("helix-build-windows", "helix-build-windows-resub");

            var runner = CreateRunner(azdo, helix, stageName: "Test");
            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Empty(helix.Resubmissions);
            Assert.Empty(azdo.UploadedJobNames);
            Assert.Empty(azdo.CreatedTestRuns);
        }

        /// <summary>
        /// The monitor times out while Helix tests are in progress.
        /// On relaunch, it should pick up where it left off: skip already-processed
        /// Helix jobs and upload results for newly-completed ones.
        /// </summary>
        [Fact]
        public async Task MonitorTimesOut_Relaunched_UploadsRemainingResults()
        {
            // --- First run: processes helix-linux, then times out while helix-windows is running ---
            var azdo1 = new FakeAzureDevOpsService();
            var helix1 = new FakeHelixService();

            // Build jobs both completed
            azdo1.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"),
                PipelineJob("Test Windows", "completed", "succeeded"));
            azdo1.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"),
                PipelineJob("Test Windows", "completed", "succeeded"));

            // Poll 1: helix-linux finished, helix-windows running
            helix1.AddResponse(
                jobs: [HelixJob("helix-linux", "finished"), HelixJob("helix-windows", "running")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["linux-wi-1"]),
                });
            // Poll 2: helix-windows still running — then the monitor times out
            helix1.AddResponse(
                jobs: [HelixJob("helix-linux", "finished"), HelixJob("helix-windows", "running")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["linux-wi-1"]),
                });

            // Simulate timeout via cancellation after 2 polls
            int pollCount1 = 0;
            using var cts = new CancellationTokenSource();
            var runner1 = new JobMonitorRunner(DefaultOptions(), NullLogger.Instance, azdo1, helix1,
                (_, ct) =>
                {
                    pollCount1++;
                    if (pollCount1 >= 2)
                    {
                        cts.Cancel();
                    }

                    return Task.CompletedTask;
                });

            int exitCode1 = await runner1.RunAsync(cts.Token);

            // Timed out → exit 1. helix-linux was uploaded, helix-windows was not.
            Assert.Equal(1, exitCode1);
            Assert.Equal(["helix-linux"], azdo1.UploadedJobNames);

            // --- Second run: monitor relaunched, helix-linux already processed ---
            var azdo2 = new FakeAzureDevOpsService();
            azdo2.WithPreviouslyProcessedJob("helix-linux");
            var helix2 = new FakeHelixService();

            azdo2.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"),
                PipelineJob("Test Windows", "completed", "succeeded"));

            // helix-windows now finished
            helix2.AddResponse(
                jobs: [HelixJob("helix-linux", "finished"), HelixJob("helix-windows", "finished")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["linux-wi-1"]),
                    ["helix-windows"] = PassFail(passed: ["win-wi-1", "win-wi-2"]),
                });

            var runner2 = CreateRunner(azdo2, helix2);
            int exitCode2 = await runner2.RunAsync(CancellationToken.None);

            // helix-linux skipped (already processed), helix-windows uploaded → exit 0
            Assert.Equal(0, exitCode2);
            Assert.Equal(["helix-windows"], azdo2.UploadedJobNames);
            Assert.Single(azdo2.CreatedTestRuns);
        }

        /// <summary>
        /// The monitor times out while some pipeline jobs haven't submitted Helix work yet,
        /// but one job's Helix results were already uploaded. On relaunch, the monitor picks up:
        /// skips already-processed Helix jobs, processes the new ones that have now completed.
        /// </summary>
        [Fact]
        public async Task MonitorTimesOut_PartialProgress_Relaunched_CompletesSuccessfully()
        {
            // --- First run ---
            // Test Linux submitted helix-linux (finished, uploaded).
            // Test Windows is still running (hasn't submitted Helix work yet).
            // Monitor times out.
            var azdo1 = new FakeAzureDevOpsService();
            var helix1 = new FakeHelixService();

            azdo1.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"),
                PipelineJob("Test Windows", "inProgress"));
            azdo1.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"),
                PipelineJob("Test Windows", "inProgress"));

            // Poll 1: helix-linux finished, no Windows Helix job yet
            helix1.AddResponse(
                jobs: [HelixJob("helix-linux", "finished")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["linux-wi-1"]),
                });
            // Poll 2: same — Windows build still running, timeout fires
            helix1.AddResponse(
                jobs: [HelixJob("helix-linux", "finished")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["linux-wi-1"]),
                });

            int pollCount1 = 0;
            using var cts = new CancellationTokenSource();
            var runner1 = new JobMonitorRunner(DefaultOptions(), NullLogger.Instance, azdo1, helix1,
                (_, ct) =>
                {
                    pollCount1++;
                    if (pollCount1 >= 2)
                    {
                        cts.Cancel();
                    }

                    return Task.CompletedTask;
                });

            int exitCode1 = await runner1.RunAsync(cts.Token);
            Assert.Equal(1, exitCode1);
            Assert.Equal(["helix-linux"], azdo1.UploadedJobNames);

            // --- Second run: monitor relaunched ---
            // Test Windows has now completed and submitted helix-windows.
            // helix-linux already processed.
            var azdo2 = new FakeAzureDevOpsService();
            azdo2.WithPreviouslyProcessedJob("helix-linux");
            var helix2 = new FakeHelixService();

            // Both build jobs done
            azdo2.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"),
                PipelineJob("Test Windows", "completed", "succeeded"));
            // helix-windows still running
            azdo2.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"),
                PipelineJob("Test Windows", "completed", "succeeded"));

            // Poll 1: helix-linux still visible (but processed), helix-windows running
            helix2.AddResponse(
                jobs: [HelixJob("helix-linux", "finished"), HelixJob("helix-windows", "running")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["linux-wi-1"]),
                });
            // Poll 2: helix-windows finished
            helix2.AddResponse(
                jobs: [HelixJob("helix-linux", "finished"), HelixJob("helix-windows", "finished")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["linux-wi-1"]),
                    ["helix-windows"] = PassFail(passed: ["win-wi-1"]),
                });

            var runner2 = CreateRunner(azdo2, helix2);
            int exitCode2 = await runner2.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode2);
            Assert.Equal(2, azdo2.TimelineCallCount);
            // Only helix-windows uploaded (helix-linux was already processed)
            Assert.Equal(["helix-windows"], azdo2.UploadedJobNames);
            Assert.Single(azdo2.CreatedTestRuns);
        }

        // -----------------------------------------------------------------------
        // Retry with Helix work item resubmission
        // -----------------------------------------------------------------------

        /// <summary>
        /// On restart after a failed attempt, the monitor finds a completed Helix job with 2
        /// failed work items. It resubmits the failed items. The resubmitted job completes with
        /// all items passing.
        /// The monitor should exit 0 (the resubmission "healed" the failures).
        /// Only the failed items should be resubmitted. Results from the original job are not
        /// uploaded by this invocation because the failed work items are being resubmitted.
        /// </summary>
        [Fact]
        public async Task RetryAttempt2_ResubmitsFailedWorkItems_ResubmissionPasses_ExitZero()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            // Pipeline jobs already completed
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"));
            // Extra polls while resubmitted job runs
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"));
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"));

            // Poll 1: original Helix job finished with 1 pass + 2 failures
            helix.AddResponse(
                jobs: [HelixJob("helix-linux", "finished")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["wi-ok"], failed: ["wi-fail-1", "wi-fail-2"]),
                });
            // Configure: resubmission of helix-linux creates "helix-linux-resub"
            helix.ConfigureResubmission("helix-linux", "helix-linux-resub");

            // Poll 2: resubmitted job appears, running
            helix.AddResponse(
                jobs: [HelixJob("helix-linux", "finished"), HelixJob("helix-linux-resub", "running", previousHelixJobName: "helix-linux")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["wi-ok"], failed: ["wi-fail-1", "wi-fail-2"]),
                });

            // Poll 3: resubmitted job finished — both items now pass
            helix.AddResponse(
                jobs: [HelixJob("helix-linux", "finished"), HelixJob("helix-linux-resub", "finished", previousHelixJobName: "helix-linux")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["wi-ok"], failed: ["wi-fail-1", "wi-fail-2"]),
                    ["helix-linux-resub"] = PassFail(passed: ["wi-fail-1", "wi-fail-2"]),
                });

            var runner = CreateRunner(azdo, helix, attempt: 2);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            // Resubmission healed the failures → exit 0
            Assert.Equal(0, exitCode);

            // Test result upload is independent from retry: original results upload before the resubmission.
            Assert.Equal(["helix-linux", "helix-linux-resub"], azdo.UploadedJobNames);
            Assert.Equal(2, azdo.CreatedTestRuns.Count);

            // Only the 2 failed items were resubmitted (not the passing one)
            Assert.Single(helix.Resubmissions);
            Assert.Equal("helix-linux", helix.Resubmissions[0].OriginalJob);
            Assert.Equal(2, helix.Resubmissions[0].FailedItems.Count);
            Assert.Contains("wi-fail-1", helix.Resubmissions[0].FailedItems);
            Assert.Contains("wi-fail-2", helix.Resubmissions[0].FailedItems);
        }

        /// <summary>
        /// On restart after a failed attempt, the monitor resubmits failed work items, but the
        /// resubmission also fails. The monitor should exit 1.
        /// </summary>
        [Fact]
        public async Task RetryAttempt2_ResubmitsFailedWorkItems_ResubmissionAlsoFails_ExitOne()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"));
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"));

            // Poll 1: original job has 1 failure
            helix.AddResponse(
                jobs: [HelixJob("helix-linux", "finished")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["wi-ok"], failed: ["wi-fail"]),
                });
            helix.ConfigureResubmission("helix-linux", "helix-linux-resub");

            // Poll 2: resubmission finished but STILL fails
            helix.AddResponse(
                jobs: [HelixJob("helix-linux", "finished"), HelixJob("helix-linux-resub", "finished", previousHelixJobName: "helix-linux")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["wi-ok"], failed: ["wi-fail"]),
                    ["helix-linux-resub"] = PassFail(failed: ["wi-fail"]),
                });

            var runner = CreateRunner(azdo, helix, attempt: 2);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            // Resubmission also failed → exit 1
            Assert.Equal(1, exitCode);
            Assert.Equal(["helix-linux", "helix-linux-resub"], azdo.UploadedJobNames);
        }

        /// <summary>
        /// On restart after a failed attempt, two Helix jobs have failures. Only the failed work
        /// items from each are resubmitted (not passing items). As resubmissions pass, the set of
        /// still-failing items shrinks. Both resubmissions pass → exit 0.
        /// </summary>
        [Fact]
        public async Task RetryAttempt2_MultipleJobs_OnlyFailedItemsResubmitted_ExitZero()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"),
                PipelineJob("Test Windows", "completed", "succeeded"));
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"),
                PipelineJob("Test Windows", "completed", "succeeded"));
            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"),
                PipelineJob("Test Windows", "completed", "succeeded"));

            // Poll 1: both original jobs finished with mixed results
            helix.AddResponse(
                jobs: [HelixJob("helix-linux", "finished"), HelixJob("helix-windows", "finished")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["linux-ok-1", "linux-ok-2"], failed: ["linux-fail"]),
                    ["helix-windows"] = PassFail(passed: ["win-ok"], failed: ["win-fail-1", "win-fail-2"]),
                });
            helix.ConfigureResubmission("helix-linux", "helix-linux-resub");
            helix.ConfigureResubmission("helix-windows", "helix-windows-resub");

            // Poll 2: linux resubmission finished (passes), windows still running
            helix.AddResponse(
                jobs:
                [
                    HelixJob("helix-linux", "finished"), HelixJob("helix-windows", "finished"),
                    HelixJob("helix-linux-resub", "finished", previousHelixJobName: "helix-linux"),
                    HelixJob("helix-windows-resub", "running", previousHelixJobName: "helix-windows"),
                ],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["linux-ok-1", "linux-ok-2"], failed: ["linux-fail"]),
                    ["helix-windows"] = PassFail(passed: ["win-ok"], failed: ["win-fail-1", "win-fail-2"]),
                    ["helix-linux-resub"] = PassFail(passed: ["linux-fail"]),
                });

            // Poll 3: windows resubmission also passes
            helix.AddResponse(
                jobs:
                [
                    HelixJob("helix-linux", "finished"), HelixJob("helix-windows", "finished"),
                    HelixJob("helix-linux-resub", "finished", previousHelixJobName: "helix-linux"),
                    HelixJob("helix-windows-resub", "finished", previousHelixJobName: "helix-windows"),
                ],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["linux-ok-1", "linux-ok-2"], failed: ["linux-fail"]),
                    ["helix-windows"] = PassFail(passed: ["win-ok"], failed: ["win-fail-1", "win-fail-2"]),
                    ["helix-linux-resub"] = PassFail(passed: ["linux-fail"]),
                    ["helix-windows-resub"] = PassFail(passed: ["win-fail-1", "win-fail-2"]),
                });

            var runner = CreateRunner(azdo, helix, attempt: 2);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode);

            // Test result upload is independent from retry: originals upload before resubmissions.
            Assert.Equal(4, azdo.CreatedTestRuns.Count);
            Assert.Equal(
                ["helix-linux", "helix-windows", "helix-linux-resub", "helix-windows-resub"],
                azdo.UploadedJobNames);

            // Each original job only had its failed items resubmitted
            Assert.Equal(2, helix.Resubmissions.Count);
            var linuxResub = helix.Resubmissions.Single(r => r.OriginalJob == "helix-linux");
            Assert.Single(linuxResub.FailedItems); // only "linux-fail"
            Assert.Contains("linux-fail", linuxResub.FailedItems);

            var windowsResub = helix.Resubmissions.Single(r => r.OriginalJob == "helix-windows");
            Assert.Equal(2, windowsResub.FailedItems.Count);
            Assert.Contains("win-fail-1", windowsResub.FailedItems);
            Assert.Contains("win-fail-2", windowsResub.FailedItems);
        }

        /// <summary>
        /// Retry is entry-only. If a Helix job is running when the monitor starts and later
        /// completes with failures, that failed work is not resubmitted until the next monitor
        /// invocation.
        /// </summary>
        [Fact]
        public async Task HelixJobFailsAfterMonitorEntry_IsNotResubmittedUntilNextEntry()
        {
            var azdo1 = new FakeAzureDevOpsService();
            var helix1 = new FakeHelixService();

            azdo1.AddTimelineResponse(MonitorJob(), PipelineJob("Test Linux", "completed", "succeeded"));
            azdo1.AddTimelineResponse(MonitorJob(), PipelineJob("Test Linux", "completed", "succeeded"));

            helix1.AddResponse(jobs: [HelixJob("helix-linux", "running")]);
            helix1.AddResponse(
                jobs: [HelixJob("helix-linux", "finished")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(failed: ["wi-fail"]),
                });

            var runner1 = CreateRunner(azdo1, helix1);
            int exitCode1 = await runner1.RunAsync(CancellationToken.None);

            Assert.Equal(1, exitCode1);
            Assert.Empty(helix1.Resubmissions);
            Assert.Equal(["helix-linux"], azdo1.UploadedJobNames);

            var azdo2 = new FakeAzureDevOpsService();
            azdo2.WithPreviouslyProcessedJob("helix-linux");
            var helix2 = new FakeHelixService();

            azdo2.AddTimelineResponse(MonitorJob(attempt: 2, previousAttempts: [PreviousAttempt(1)]), PipelineJob("Test Linux", "completed", "succeeded"));
            azdo2.AddTimelineResponse(MonitorJob(attempt: 2, previousAttempts: [PreviousAttempt(1)]), PipelineJob("Test Linux", "completed", "succeeded"));

            helix2.AddResponse(
                jobs: [HelixJob("helix-linux", "finished")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(failed: ["wi-fail"]),
                });
            helix2.ConfigureResubmission("helix-linux", "helix-linux-resub");
            helix2.AddResponse(
                jobs: [HelixJob("helix-linux", "finished"), HelixJob("helix-linux-resub", "finished", previousHelixJobName: "helix-linux")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux-resub"] = PassFail(passed: ["wi-fail"]),
                });

            var runner2 = CreateRunner(azdo2, helix2, attempt: 2);
            int exitCode2 = await runner2.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode2);
            Assert.Single(helix2.Resubmissions);
            Assert.Equal("helix-linux", helix2.Resubmissions[0].OriginalJob);
            Assert.Equal(["wi-fail"], helix2.Resubmissions[0].FailedItems);
            Assert.Equal(["helix-linux-resub"], azdo2.UploadedJobNames);
        }

        /// <summary>
        /// If the monitor cold-starts after a newer incarnation already passed, older failed
        /// Helix jobs are not resubmitted. Test results still upload old-to-new.
        /// </summary>
        [Fact]
        public async Task NewerPassedIncarnationExistsOnEntry_DoesNotResubmitOlderFailure_ExitZero()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            azdo.AddTimelineResponse(MonitorJob(), PipelineJob("Test Linux", "completed", "succeeded"));

            helix.AddResponse(
                jobs:
                [
                    HelixJob("helix-linux", "finished"),
                    HelixJob("helix-linux-resub", "finished", previousHelixJobName: "helix-linux"),
                ],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(failed: ["wi-fail"]),
                    ["helix-linux-resub"] = PassFail(passed: ["wi-fail"]),
                });
            helix.ConfigureResubmission("helix-linux", "helix-linux-resub-2");
            helix.ConfigureResubmission("helix-linux-resub", "helix-linux-resub-2");

            var runner = CreateRunner(azdo, helix);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Empty(helix.Resubmissions);
            Assert.Equal(["helix-linux", "helix-linux-resub"], azdo.UploadedJobNames);
        }

        /// <summary>
        /// If the latest incarnation is still running, older failed incarnations are not
        /// resubmitted again. The old results still upload once, and the monitor waits for the
        /// running latest incarnation.
        /// </summary>
        [Fact]
        public async Task NewerRunningIncarnationExistsOnEntry_DoesNotResubmitOlderFailure_ExitZero()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            azdo.AddTimelineResponse(MonitorJob(), PipelineJob("Test Linux", "completed", "succeeded"));
            azdo.AddTimelineResponse(MonitorJob(), PipelineJob("Test Linux", "completed", "succeeded"));

            helix.AddResponse(
                jobs:
                [
                    HelixJob("helix-linux", "finished"),
                    HelixJob("helix-linux-resub", "running", previousHelixJobName: "helix-linux"),
                ],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(failed: ["wi-fail"]),
                });
            helix.ConfigureResubmission("helix-linux", "helix-linux-resub-2");
            helix.AddResponse(
                jobs:
                [
                    HelixJob("helix-linux", "finished"),
                    HelixJob("helix-linux-resub", "finished", previousHelixJobName: "helix-linux"),
                ],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux-resub"] = PassFail(passed: ["wi-fail"]),
                });

            var runner = CreateRunner(azdo, helix);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Empty(helix.Resubmissions);
            Assert.Equal(["helix-linux", "helix-linux-resub"], azdo.UploadedJobNames);
        }

        /// <summary>
        /// When a retry partially heals prior failures, the next monitor entry resubmits only the
        /// items that failed in the latest completed incarnation.
        /// </summary>
        [Fact]
        public async Task LatestCompletedIncarnationPartiallyHealed_ResubmitsOnlyRemainingFailures()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            azdo.AddTimelineResponse(MonitorJob(), PipelineJob("Test Linux", "completed", "succeeded"));
            azdo.AddTimelineResponse(MonitorJob(), PipelineJob("Test Linux", "completed", "succeeded"));

            helix.AddResponse(
                jobs:
                [
                    HelixJob("helix-linux", "finished"),
                    HelixJob("helix-linux-resub", "finished", previousHelixJobName: "helix-linux"),
                ],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(failed: ["wi-1", "wi-2"]),
                    ["helix-linux-resub"] = PassFail(passed: ["wi-1"], failed: ["wi-2"]),
                });
            helix.ConfigureResubmission("helix-linux-resub", "helix-linux-resub-2");
            helix.AddResponse(
                jobs:
                [
                    HelixJob("helix-linux", "finished"),
                    HelixJob("helix-linux-resub", "finished", previousHelixJobName: "helix-linux"),
                    HelixJob("helix-linux-resub-2", "finished", previousHelixJobName: "helix-linux-resub"),
                ],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(failed: ["wi-1", "wi-2"]),
                    ["helix-linux-resub"] = PassFail(passed: ["wi-1"], failed: ["wi-2"]),
                    ["helix-linux-resub-2"] = PassFail(passed: ["wi-2"]),
                });

            var runner = CreateRunner(azdo, helix);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Single(helix.Resubmissions);
            Assert.Equal("helix-linux-resub", helix.Resubmissions[0].OriginalJob);
            Assert.Equal(["wi-2"], helix.Resubmissions[0].FailedItems);
            Assert.Equal(["helix-linux", "helix-linux-resub", "helix-linux-resub-2"], azdo.UploadedJobNames);
        }

        /// <summary>
        /// Helix snapshots are not guaranteed to be returned in lineage order. Upload still needs
        /// to publish older jobs before newer retry incarnations.
        /// </summary>
        [Fact]
        public async Task CompletedHelixJobsReturnedOutOfOrder_UploadsOldToNew()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            azdo.AddTimelineResponse(MonitorJob(), PipelineJob("Test Linux", "completed", "succeeded"));

            helix.AddResponse(
                jobs:
                [
                    HelixJob("helix-linux-resub", "finished", previousHelixJobName: "helix-linux"),
                    HelixJob("helix-linux", "finished"),
                ],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(failed: ["wi-fail"]),
                    ["helix-linux-resub"] = PassFail(passed: ["wi-fail"]),
                });

            var runner = CreateRunner(azdo, helix);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode);
            Assert.Equal(["helix-linux", "helix-linux-resub"], azdo.UploadedJobNames);
        }

        /// <summary>
        /// A, B, and C start in the first invocation. A and B submit Helix jobs. A's AzDO job
        /// succeeds but its Helix work fails and gets uploaded. B's AzDO job fails after
        /// submitting Helix work, and the monitor exits before B produces test results because C
        /// also fails. On retry, A and B's failed Helix work items should be resubmitted, while C
        /// is restarted as an AzDO job. On the following retry, only B's still-failed resubmission
        /// is submitted again because A's resubmission already passed.
        /// </summary>
        [Fact]
        public async Task RetryAfterMixedAzDOAndHelixFailures_RestartsFailedHelixWorkAndFailedAzDOJob()
        {
            var azdo1 = new FakeAzureDevOpsService();
            var helix1 = new FakeHelixService();

            azdo1.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("A", "pending"),
                PipelineJob("B", "pending"),
                PipelineJob("C", "pending"));
            azdo1.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("A", "inProgress"),
                PipelineJob("B", "inProgress"),
                PipelineJob("C", "pending"));
            azdo1.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("A", "inProgress"),
                PipelineJob("B", "inProgress"),
                PipelineJob("C", "pending"));
            azdo1.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("A", "inProgress"),
                PipelineJob("B", "inProgress"),
                PipelineJob("C", "inProgress"));
            azdo1.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("A", "completed", "succeeded"),
                PipelineJob("B", "completed", "failed"),
                PipelineJob("C", "inProgress"));
            azdo1.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("A", "completed", "succeeded"),
                PipelineJob("B", "completed", "failed"),
                PipelineJob("C", "completed", "failed"));

            helix1.AddResponse(jobs: []);
            helix1.AddResponse(jobs: []);
            helix1.AddResponse(jobs: [HelixJob("helix-a", "running", submitterJobName: "A"), HelixJob("helix-b", "running", submitterJobName: "B")]);
            helix1.AddResponse(jobs: [HelixJob("helix-a", "running", submitterJobName: "A"), HelixJob("helix-b", "running", submitterJobName: "B")]);
            helix1.AddResponse(
                jobs: [HelixJob("helix-a", "finished", submitterJobName: "A"), HelixJob("helix-b", "running", submitterJobName: "B")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-a"] = PassFail(failed: ["a-fail"]),
                });
            helix1.AddResponse(
                jobs: [HelixJob("helix-a", "finished", submitterJobName: "A"), HelixJob("helix-b", "running", submitterJobName: "B")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-a"] = PassFail(failed: ["a-fail"]),
                });

            var runner1 = CreateRunner(azdo1, helix1);
            int exitCode1 = await runner1.RunAsync(CancellationToken.None);

            Assert.Equal(1, exitCode1);
            Assert.Equal(["helix-a"], azdo1.UploadedJobNames);
            Assert.Empty(helix1.Resubmissions);

            var azdo2 = new FakeAzureDevOpsService();
            azdo2.WithPreviouslyProcessedJob("helix-a");
            var helix2 = new FakeHelixService();

            azdo2.AddTimelineResponse(
                MonitorJob(attempt: 2, previousAttempts: [PreviousAttempt(1)]),
                PipelineJob("A", "completed", "succeeded"),
                PipelineJob("B", "completed", "failed"),
                PipelineJob("C", "inProgress", attempt: 2, previousAttempts: [PreviousAttempt(1)]));
            azdo2.AddTimelineResponse(
                MonitorJob(attempt: 2, previousAttempts: [PreviousAttempt(1)]),
                PipelineJob("A", "completed", "succeeded"),
                PipelineJob("B", "completed", "failed"),
                PipelineJob("C", "completed", "succeeded", attempt: 2, previousAttempts: [PreviousAttempt(1)]));

            helix2.AddResponse(
                jobs: [HelixJob("helix-a", "finished", submitterJobName: "A"), HelixJob("helix-b", "finished", submitterJobName: "B")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-a"] = PassFail(failed: ["a-fail"]),
                    ["helix-b"] = PassFail(failed: ["b-fail"]),
                });
            helix2.ConfigureResubmission("helix-a", "helix-a-resub");
            helix2.ConfigureResubmission("helix-b", "helix-b-resub");
            helix2.AddResponse(
                jobs:
                [
                    HelixJob("helix-a", "finished", submitterJobName: "A"),
                    HelixJob("helix-b", "finished", submitterJobName: "B"),
                    HelixJob("helix-a-resub", "finished", submitterJobName: "A", previousHelixJobName: "helix-a"),
                    HelixJob("helix-b-resub", "finished", submitterJobName: "B", previousHelixJobName: "helix-b"),
                ],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-a-resub"] = PassFail(passed: ["a-fail"]),
                    ["helix-b-resub"] = PassFail(failed: ["b-fail"]),
                });

            var runner2 = CreateRunner(azdo2, helix2, attempt: 2);
            int exitCode2 = await runner2.RunAsync(CancellationToken.None);

            Assert.Equal(1, exitCode2);
            Assert.Equal(2, azdo2.TimelineCallCount);
            Assert.Equal(["helix-b", "helix-a-resub", "helix-b-resub"], azdo2.UploadedJobNames);

            Assert.Equal(2, helix2.Resubmissions.Count);
            Assert.Equal(["a-fail"], helix2.Resubmissions.Single(r => r.OriginalJob == "helix-a").FailedItems);
            Assert.Equal(["b-fail"], helix2.Resubmissions.Single(r => r.OriginalJob == "helix-b").FailedItems);

            var azdo3 = new FakeAzureDevOpsService();
            azdo3.WithPreviouslyProcessedJob("helix-a");
            azdo3.WithPreviouslyProcessedJob("helix-b");
            azdo3.WithPreviouslyProcessedJob("helix-a-resub");
            azdo3.WithPreviouslyProcessedJob("helix-b-resub");
            var helix3 = new FakeHelixService();

            azdo3.AddTimelineResponse(
                MonitorJob(attempt: 3, previousAttempts: [PreviousAttempt(1), PreviousAttempt(2)]),
                PipelineJob("A", "completed", "succeeded"),
                PipelineJob("B", "completed", "failed"),
                PipelineJob("C", "completed", "succeeded", attempt: 2, previousAttempts: [PreviousAttempt(1)]));
            azdo3.AddTimelineResponse(
                MonitorJob(attempt: 3, previousAttempts: [PreviousAttempt(1), PreviousAttempt(2)]),
                PipelineJob("A", "completed", "succeeded"),
                PipelineJob("B", "completed", "failed"),
                PipelineJob("C", "completed", "succeeded", attempt: 2, previousAttempts: [PreviousAttempt(1)]));

            helix3.AddResponse(
                jobs:
                [
                    HelixJob("helix-a", "finished", submitterJobName: "A"),
                    HelixJob("helix-b", "finished", submitterJobName: "B"),
                    HelixJob("helix-a-resub", "finished", submitterJobName: "A", previousHelixJobName: "helix-a"),
                    HelixJob("helix-b-resub", "finished", submitterJobName: "B", previousHelixJobName: "helix-b"),
                ],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-a-resub"] = PassFail(passed: ["a-fail"]),
                    ["helix-b-resub"] = PassFail(failed: ["b-fail"]),
                });
            helix3.ConfigureResubmission("helix-b-resub", "helix-b-resub-2");
            helix3.AddResponse(
                jobs:
                [
                    HelixJob("helix-a", "finished", submitterJobName: "A"),
                    HelixJob("helix-b", "finished", submitterJobName: "B"),
                    HelixJob("helix-a-resub", "finished", submitterJobName: "A", previousHelixJobName: "helix-a"),
                    HelixJob("helix-b-resub", "finished", submitterJobName: "B", previousHelixJobName: "helix-b"),
                    HelixJob("helix-b-resub-2", "finished", submitterJobName: "B", previousHelixJobName: "helix-b-resub"),
                ],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-b-resub-2"] = PassFail(passed: ["b-fail"]),
                });

            var runner3 = CreateRunner(azdo3, helix3, attempt: 3);
            int exitCode3 = await runner3.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode3);
            Assert.Single(helix3.Resubmissions);
            Assert.Equal("helix-b-resub", helix3.Resubmissions[0].OriginalJob);
            Assert.Equal(["b-fail"], helix3.Resubmissions[0].FailedItems);
            Assert.Equal(["helix-b-resub-2"], azdo3.UploadedJobNames);
        }

        /// <summary>
        /// A and B submit Helix jobs before the monitor starts. A's work item fails before the
        /// first monitor invocation, so A is resubmitted on entry and A's original failed results
        /// are still uploaded. After the monitor crashes, A's resubmission is still waiting, so it
        /// is not resubmitted again. After another crash, B fails while the monitor is down; the
        /// next monitor entry resubmits only B. Once A's resubmission and B's resubmission pass,
        /// the monitor exits successfully.
        /// </summary>
        [Fact]
        public async Task RetryOnEntryWithCrashes_ResubmitsOnlyLatestFailedWork()
        {
            var azdo1 = new FakeAzureDevOpsService();
            var helix1 = new FakeHelixService();

            azdo1.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("A", "completed", "succeeded"),
                PipelineJob("B", "completed", "succeeded"));

            helix1.AddResponse(
                jobs: [HelixJob("helix-a", "finished", submitterJobName: "A"), HelixJob("helix-b", "running", submitterJobName: "B")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-a"] = PassFail(failed: ["a-fail"]),
                });
            helix1.ConfigureResubmission("helix-a", "helix-a-resub");
            helix1.AddResponse(
                jobs:
                [
                    HelixJob("helix-a", "finished", submitterJobName: "A"),
                    HelixJob("helix-a-resub", "running", submitterJobName: "A", previousHelixJobName: "helix-a"),
                    HelixJob("helix-b", "running", submitterJobName: "B"),
                ],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-a"] = PassFail(failed: ["a-fail"]),
                });

            using var cts1 = new CancellationTokenSource();
            var runner1 = new JobMonitorRunner(DefaultOptions(), NullLogger.Instance, azdo1, helix1,
                (_, _) =>
                {
                    cts1.Cancel();
                    return Task.CompletedTask;
                });

            int exitCode1 = await runner1.RunAsync(cts1.Token);

            Assert.Equal(1, exitCode1);
            Assert.Single(helix1.Resubmissions);
            Assert.Equal("helix-a", helix1.Resubmissions[0].OriginalJob);
            Assert.Equal(["a-fail"], helix1.Resubmissions[0].FailedItems);
            Assert.Equal(["helix-a"], azdo1.UploadedJobNames);

            var azdo2 = new FakeAzureDevOpsService();
            azdo2.WithPreviouslyProcessedJob("helix-a");
            var helix2 = new FakeHelixService();

            azdo2.AddTimelineResponse(
                MonitorJob(attempt: 2, previousAttempts: [PreviousAttempt(1)]),
                PipelineJob("A", "completed", "succeeded"),
                PipelineJob("B", "completed", "succeeded"));

            helix2.AddResponse(
                jobs:
                [
                    HelixJob("helix-a", "finished", submitterJobName: "A"),
                    HelixJob("helix-a-resub", "running", submitterJobName: "A", previousHelixJobName: "helix-a"),
                    HelixJob("helix-b", "running", submitterJobName: "B"),
                ]);
            helix2.ConfigureResubmission("helix-a-resub", "helix-a-resub-2");
            helix2.ConfigureResubmission("helix-b", "helix-b-resub");
            helix2.AddResponse(
                jobs:
                [
                    HelixJob("helix-a", "finished", submitterJobName: "A"),
                    HelixJob("helix-a-resub", "running", submitterJobName: "A", previousHelixJobName: "helix-a"),
                    HelixJob("helix-b", "running", submitterJobName: "B"),
                ]);

            using var cts2 = new CancellationTokenSource();
            var runner2 = new JobMonitorRunner(DefaultOptions(attempt: 2), NullLogger.Instance, azdo2, helix2,
                (_, _) =>
                {
                    cts2.Cancel();
                    return Task.CompletedTask;
                });

            int exitCode2 = await runner2.RunAsync(cts2.Token);

            Assert.Equal(1, exitCode2);
            Assert.Empty(helix2.Resubmissions);
            Assert.Empty(azdo2.UploadedJobNames);

            var azdo3 = new FakeAzureDevOpsService();
            azdo3.WithPreviouslyProcessedJob("helix-a");
            var helix3 = new FakeHelixService();

            azdo3.AddTimelineResponse(
                MonitorJob(attempt: 3, previousAttempts: [PreviousAttempt(1), PreviousAttempt(2)]),
                PipelineJob("A", "completed", "succeeded"),
                PipelineJob("B", "completed", "succeeded"));
            azdo3.AddTimelineResponse(
                MonitorJob(attempt: 3, previousAttempts: [PreviousAttempt(1), PreviousAttempt(2)]),
                PipelineJob("A", "completed", "succeeded"),
                PipelineJob("B", "completed", "succeeded"));

            helix3.AddResponse(
                jobs:
                [
                    HelixJob("helix-a", "finished", submitterJobName: "A"),
                    HelixJob("helix-a-resub", "running", submitterJobName: "A", previousHelixJobName: "helix-a"),
                    HelixJob("helix-b", "finished", submitterJobName: "B"),
                ],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-b"] = PassFail(failed: ["b-fail"]),
                });
            helix3.ConfigureResubmission("helix-b", "helix-b-resub");
            helix3.AddResponse(
                jobs:
                [
                    HelixJob("helix-a", "finished", submitterJobName: "A"),
                    HelixJob("helix-a-resub", "running", submitterJobName: "A", previousHelixJobName: "helix-a"),
                    HelixJob("helix-b", "finished", submitterJobName: "B"),
                    HelixJob("helix-b-resub", "running", submitterJobName: "B", previousHelixJobName: "helix-b"),
                ],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-b"] = PassFail(failed: ["b-fail"]),
                });
            helix3.AddResponse(
                jobs:
                [
                    HelixJob("helix-a", "finished", submitterJobName: "A"),
                    HelixJob("helix-a-resub", "finished", submitterJobName: "A", previousHelixJobName: "helix-a"),
                    HelixJob("helix-b", "finished", submitterJobName: "B"),
                    HelixJob("helix-b-resub", "finished", submitterJobName: "B", previousHelixJobName: "helix-b"),
                ],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-a-resub"] = PassFail(passed: ["a-fail"]),
                    ["helix-b"] = PassFail(failed: ["b-fail"]),
                    ["helix-b-resub"] = PassFail(passed: ["b-fail"]),
                });

            var runner3 = CreateRunner(azdo3, helix3, attempt: 3);
            int exitCode3 = await runner3.RunAsync(CancellationToken.None);

            Assert.Equal(0, exitCode3);
            Assert.Single(helix3.Resubmissions);
            Assert.Equal("helix-b", helix3.Resubmissions[0].OriginalJob);
            Assert.Equal(["b-fail"], helix3.Resubmissions[0].FailedItems);
            Assert.Equal(["helix-b", "helix-a-resub", "helix-b-resub"], azdo3.UploadedJobNames);
        }

        /// <summary>
        /// Failed work items that already exist when the monitor starts are resubmitted even on
        /// the monitor's first attempt. The original failed results are still uploaded normally.
        /// </summary>
        [Fact]
        public async Task Attempt1_ResubmitsFailedWorkItemsFoundOnEntry_ExitOne()
        {
            var azdo = new FakeAzureDevOpsService();
            var helix = new FakeHelixService();

            azdo.AddTimelineResponse(
                MonitorJob(),
                PipelineJob("Test Linux", "completed", "succeeded"));

            helix.AddResponse(
                jobs: [HelixJob("helix-linux", "finished")],
                passFailByJob: new(StringComparer.OrdinalIgnoreCase)
                {
                    ["helix-linux"] = PassFail(passed: ["wi-ok"], failed: ["wi-fail"]),
                });

            var runner = CreateRunner(azdo, helix);
            int exitCode = await runner.RunAsync(CancellationToken.None);

            Assert.Equal(1, exitCode);
            Assert.Single(helix.Resubmissions);
            Assert.Equal("helix-linux", helix.Resubmissions[0].OriginalJob);
            Assert.Equal(["wi-fail"], helix.Resubmissions[0].FailedItems);
            Assert.Single(azdo.UploadedJobNames); // only original
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static JobMonitorOptions DefaultOptions(int attempt = 1) => new()
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
            Attempt = attempt,
        };

        private static readonly Func<TimeSpan, CancellationToken, Task> NoDelay = (_, _) => Task.CompletedTask;

        private static JobMonitorRunner CreateRunner(FakeAzureDevOpsService azdo, FakeHelixService helix, string stageName = null, int attempt = 1)
        {
            var options = DefaultOptions(attempt);
            if (stageName != null)
            {
                options.MonitorAllStages = false;
                options.StageName = stageName;
            }

            return new(options, NullLogger.Instance, azdo, helix, NoDelay);
        }
    }
}
