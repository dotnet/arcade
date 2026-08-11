# RetryPlanner

The planner performs exactly one retry pass per monitor invocation.

## Inputs

It reads all Helix jobs for the build, filters them to the configured stage, seeds lineage state,
and optionally reads failed-test metadata from completed Azure DevOps runs.

## Selection

Jobs are reduced to one latest incarnation per logical stream:

- monitor resubmission links collapse to the lineage leaf;
- unlinked rerun duplicates collapse by submitter job and queue;
- the numerically highest stage attempt breaks ties.

Current-attempt in-flight work is never duplicated. Previous-attempt unfinished work and completed
latest incarnations with failures are candidates.

## Resubmission

Only failed or unfinished work-item entries are copied into the new Helix job list. Test-only
failures are included when configured. The service preserves source, queue, payload references,
container metadata, and submitter properties, adds `PreviousHelixJobName`, and stamps the current
stage attempt.

Successful resubmission records the submitter job name so that its original Azure DevOps failure
does not independently fail the monitor while replacement Helix work is active.

If required previous-attempt work cannot be resubmitted, the planner records each item as
abandoned. This makes the monitor fail with actionable output rather than waiting forever.

The planner does not poll, upload results, or retry work first observed after entry.
