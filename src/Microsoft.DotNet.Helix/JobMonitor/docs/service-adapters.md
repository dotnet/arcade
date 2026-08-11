# Service adapters

## HelixService

The Helix adapter filters build jobs, lists work items, downloads result files, cancels jobs, and
constructs resubmissions. Generated client models are converted to `HelixJobInfo` at the boundary.

Retry-safe Helix requests use `RetryExecutor`. Result downloads continue after individual file
failures so all files are attempted; transient failures are aggregated after the batch so the
whole read phase can retry.

Resubmission reuses original payload and correlation blobs, filters the original job list to the
selected work items, preserves execution metadata, and uses the Helix idempotency key for job
creation.

## AzureDevOpsService

The Azure DevOps adapter reads the stage timeline and durable test-run metadata, creates and
completes test runs, and invokes the JobMonitor's `AzureDevOpsResultPublisher`.

Build-scoped `vstmr` tags recover processed Helix job IDs. A per-run JSON attachment recovers work
items whose uploaded tests failed.

All work-item publishers across all active job uploads share one `AsyncConcurrencyLimiter`, so
`--test-result-upload-parallelism` is a global maximum rather than a per-job multiplier.

HTTP reads and other retry-safe requests use `RetryExecutor` and honor Azure DevOps rate-limit
headers. Ambiguous lifecycle writes remain one-shot.

## TestResults

The `ResultPublishing` component is part of the JobMonitor project rather than a separate assembly. It
contains result-file readers, name formatting, rerun/data-driven aggregation, Azure DevOps wire
models, and publication. Keeping these types together preserves a clear internal boundary without
creating a project and package boundary for a subsystem with one production consumer.
