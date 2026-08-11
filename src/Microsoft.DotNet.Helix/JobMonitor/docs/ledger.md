# MonitorLedger

`MonitorLedger` is the invocation's thread-safe state machine. It contains no network calls,
filesystem operations, or logging.

## Job and lineage state

The ledger caches the latest observation of every job by Helix job name. This cache resolves
`PreviousHelixJobName` links and computes stable logical stream keys. Static ordering helpers
select lineage leaves and order jobs oldest-to-newest.

## Upload state

Each job moves through `Queued`, `InProgress`, `DurablyCompleted`, or `Failed`. Jobs discovered
from Azure DevOps tags start as durably completed. Only successful completion/tagging increments
the processed count.

## Outcome state

Outcomes are keyed by logical stream and work-item name. Reconciliation is idempotent per Helix
job. Newer incarnations overwrite older outcomes, and a later pass removes prior failure console
information.

Upload workers may add test-only failures after the poll loop records Helix exit-code outcomes.
Late results from a superseded incarnation are ignored when its replacement has already been
reconciled.

## Reporting state

The ledger stores stable snapshots of timeline records, failed-work-item console information,
deduplicated warning keys, retrying submitter names, and summary counters. Callers never enumerate
mutable internal collections.
