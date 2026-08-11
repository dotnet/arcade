# Helix Job Monitor

This directory is the authoritative design documentation for the monitor.

- [Semantic behavior](semantics.md) defines the externally visible contract.
- [Architecture and concurrency](architecture.md) describes data flow, ownership, and performance.
- [CLI and configuration](cli.md) covers process lifetime, options, defaults, and exit codes.
- [JobMonitorRunner](runner.md) describes lifecycle composition.
- [RetryPlanner](retry-planner.md) describes one-shot cross-attempt reconciliation.
- [MonitorPoller](poller.md) describes coherent snapshot acquisition.
- [MonitorLedger](ledger.md) describes durable-state reconstruction and in-memory outcomes.
- [TestResultUploadPipeline](upload-pipeline.md) describes asynchronous publication and recovery.
- [StatusReporter](reporter.md) describes logging and downstream parser contracts.
- [Service adapters](service-adapters.md) describes Helix and Azure DevOps boundaries.
- [Shared utilities](shared-utilities.md) describes bounded queues, shared concurrency, and retries.

The short `JobMonitorRunner.Design.md` at the component root intentionally only links here.
