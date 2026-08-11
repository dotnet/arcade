# JobMonitorRunner

The runner is the lifecycle composition root.

## Startup

1. Log the monitored build and stage.
2. Read completed Helix-job tags from Azure DevOps into the ledger.
3. Execute the one-shot `RetryPlanner`.
4. Seed the first poll with the entry snapshot plus any resubmissions.

## Poll loop

For every `MonitorSnapshot`, the runner:

1. updates timeline and job observations in the ledger;
2. reconciles completed jobs oldest-to-newest;
3. queues each unprocessed job for upload at most once;
4. emits status when counts change, verbose mode is enabled, or five minutes elapsed;
5. checks Azure DevOps and current-attempt Helix completion;
6. waits at least five seconds before another poll.

The runner does not refetch work items during reconciliation or reporting. Both consume the
snapshot.

## Normal completion

The runner completes and drains the upload channel, emits final failure details and summary, then
applies the documented exit precedence.

## Cancellation

Cancellation abandons the upload channel before timeout reporting. The runner then best-effort
cancels the latest unfinished lineage leaves using a fresh 30-second budget. It never waits for
upload drain on this path.

## Disposal

The runner owns the upload pipeline and production service adapters. Disposal releases those
resources; durable correctness never depends on disposal completing.
