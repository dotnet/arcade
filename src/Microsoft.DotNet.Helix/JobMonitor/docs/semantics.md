# Semantic behavior

## Purpose

The monitor is the stage-level coordinator between Azure DevOps and Helix. It discovers the Helix
jobs submitted by the stage, retries eligible work once, publishes test results, waits for all
relevant work, and returns a build result.

## Scope and stage attempts

All Helix jobs with an empty `System.StageName` or the configured stage name participate in
upload, retry, and outcome reconciliation. Jobs from another stage are ignored.

The monitor distinguishes two scopes:

- **All attempts** are considered when discovering work, selecting the latest logical
  incarnation, uploading results, and computing outcomes.
- **The current attempt** gates termination. A job is current when either attempt is unknown or
  its `System.StageAttempt` equals the monitor attempt.

Only a numerically lower attempt is a previous attempt. A higher attempt is never treated as
abandoned previous work. If the monitor attempt is unknown, every stage job gates completion.

## Logical work and latest-wins behavior

A logical stream is identified by `System.JobName + QueueId`. This keeps matrix legs that target
different Helix queues independent. If submitter identity is missing, the root of the
`PreviousHelixJobName` lineage is used.

Within a stream:

1. A resubmission lineage leaf supersedes its predecessors.
2. Unlinked stage-rerun duplicates are ordered by stage attempt.
3. Outcomes are applied oldest to newest, so the newest completed incarnation wins.
4. A later pass clears an earlier failure for the same logical work item.

## One-shot retry

Retry runs exactly once, before polling. It is derived from a single all-attempt Helix snapshot.

- A current-attempt job still in flight is left alone.
- A previous-attempt job that is unfinished is resubmitted into the current attempt.
- A completed latest incarnation resubmits only work items that have not passed.
- When enabled, prior Azure DevOps test failures also make an exit-code-zero work item eligible.
- A previous-attempt item that must run but cannot be resubmitted becomes an explicit hard failure.

Failures first observed after entry are not retried by the same invocation.

## Completion and outcomes

A Helix job is complete when Helix reports `finished` or `failed`. As a fallback, it is complete
when the expected work-item count is known and positive, enough items have appeared, and every
item has an exit code.

A work item fails when its exit code is non-zero or its state is not `Finished`. Test-result
publication may additionally mark it failed when `--fail-on-failed-tests` is enabled.

## Durable result publication

The durable upload marker is the Azure DevOps test-run tag
`helixjob<guid-without-dashes>`. It is applied only when the run is completed. Therefore:

- A tagged job is not uploaded again, but its outcomes are still reconstructed.
- An interrupted, untagged upload is replayed by a later monitor invocation.
- Failed-work-item names are attached before completion/tagging, so retry metadata cannot lag the
  durable marker.

For each job, publication is ordered:

1. Download recognized Helix result files.
2. Create an in-progress Azure DevOps test run.
3. Publish work-item results.
4. Attach failed-work-item metadata when needed.
5. Complete and tag the run.

Download reads retry transient failures. Ambiguous create, publish, and completion writes are not
replayed by the monitor. Publication failure is warning-only and never changes the build outcome.

## Termination and exit code

Normal completion requires every non-monitor Azure DevOps job and every current-attempt Helix job
to be complete. The monitor then drains accepted uploads and evaluates failures in this order:

1. A non-monitor Azure DevOps job failed or was canceled.
2. No Helix jobs were associated with the stage and `--allow-no-helix-jobs` is false.
3. The latest logical outcome of any work item failed.
4. Otherwise the monitor succeeds.

Cancellation immediately emits timeout diagnostics, abandons uploads without draining, and uses
a new 30-second budget to cancel latest in-flight Helix jobs. Cancellation exits `1`.
