# Helix Job Monitor design

The job monitor is a restart-safe coordinator between Azure DevOps and Helix. It
polls control-plane state, reconciles retries, publishes large test-result sets,
and computes the final stage outcome without tying progress reporting to result
upload latency.

The design is split by concern:

- [Semantic behavior](SemanticBehavior.md) defines externally
  observable behavior and restart invariants.
- [Architecture](Architecture.md) describes process structure, ownership,
  backpressure, and shared parallelism utilities.
- [Polling and reconciliation](Components/Polling.md) describes discovery,
  stage-attempt scoping, retry, and outcome ordering.
- [Upload pipeline](Components/UploadPipeline.md) describes the bounded
  job/work-item/finalization stages and durable completion boundary.
- [Test-result processing](Components/TestResults.md) describes XML parsing,
  aggregation, batching, attachments, and Azure DevOps limits.
- [State and status](Components/StateAndStatus.md) describes thread-safe state,
  snapshots, progress, and bounded verbose logging.
- [Performance metrics](Components/PerformanceMetrics.md) describes request
  counts, retries, throughput, rate-limit waits, and pipeline stage timings.
- [Shutdown](Components/Shutdown.md) describes normal drain, cancellation, and
  crash recovery.

## Performance goals

The monitor is designed for hundreds of Helix jobs, thousands of work items,
and millions of test results.

1. Polling and status reporting never wait for result downloads or Azure DevOps
   result uploads.
2. Every producer/consumer boundary is bounded.
3. Parallelism is global per stage, not multiplied independently per Helix job.
4. Result XML is read forward-only; complete XML documents are never retained.
5. Azure DevOps requests contain up to 1,000 top-level results, independent of
   nested sub-result count.
6. Normal drain should contain only the upload tail that could not overlap
   polling. Runtime validation is used to tune the default parallelism and
   verify that the tail remains a small fraction of the monitor duration.
