// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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

        private static JobMonitorRunner CreateRunner(FakeAzureDevOpsService azdo, FakeHelixService helix, string stageName = null)
        {
            var options = DefaultOptions();
            if (stageName != null)
            {
                options.MonitorAllStages = false;
                options.StageName = stageName;
            }

            return new(options, NullLogger.Instance, azdo, helix, NoDelay);
        }
    }
}
