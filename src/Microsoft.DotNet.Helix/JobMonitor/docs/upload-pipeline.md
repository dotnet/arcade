# TestResultUploadPipeline

Completed, previously untagged jobs enter a bounded `AsyncWorkQueue<UploadRequest>`. Queue capacity
applies backpressure to the poll loop and fixed workers prevent unbounded job-level fan-out.

## Worker sequence

1. Mark the job upload in progress.
2. Download recognized result files with bounded transient retry.
3. Create one Azure DevOps test run without retrying the ambiguous write.
4. Publish work-item results under the service-wide concurrency budget.
5. Apply test-only failures to the ledger when enabled.
6. Attach failed-work-item names, complete the run, and apply the durable Helix tag.
7. Mark the job durably processed.

Expected service failures are logged with an Azure DevOps warning decoration, leave the run
untagged, mark the invocation upload state failed, and do not fail the build. Queue infrastructure
faults are not swallowed; normal drain surfaces them.

The pending map exists only for verbose heartbeat diagnostics. It does not determine durability.

Normal completion closes the producer side and drains every accepted request. Cancellation calls
`Abandon`, which stops workers without promising to process queued requests. Any request that did
not reach completion/tagging is rediscovered by a later invocation.
