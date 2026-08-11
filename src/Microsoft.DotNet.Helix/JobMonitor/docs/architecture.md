# Architecture and concurrency

## Component flow

```text
Program
  -> JobMonitorRunner
       -> RetryPlanner -> Helix/Azure DevOps adapters
       -> MonitorPoller -> MonitorSnapshot
       -> MonitorLedger
       -> TestResultUploadPipeline -> Helix/Azure DevOps adapters
       -> StatusReporter
```

`JobMonitorRunner` is the composition root and lifecycle loop. It does not implement lineage,
network retry, snapshot collection, or upload details.

`RetryPlanner` performs the single entry reconciliation. `MonitorPoller` creates one coherent
snapshot per iteration. `MonitorLedger` owns deterministic invocation state. The reporter renders
only data already present in snapshots and ledger snapshots. Service adapters isolate generated
Helix clients, HTTP, blob storage, and filesystem access. Test-result parsing, aggregation, and
Azure DevOps publication live in the JobMonitor `ResultPublishing` component because the monitor is
their only production consumer.

## Snapshot model

Each poll produces one immutable `MonitorSnapshot` containing:

- stage-scoped Azure DevOps timeline records;
- all stage-scoped Helix jobs;
- exactly one work-item listing per job for that poll;
- the set of jobs considered complete.

Completion, reconciliation, status counts, and verbose output all consume the same work-item
listing. This removes duplicate network calls and ensures one poll cannot combine observations
from different moments. Terminal job listings are cached across polls; only in-flight jobs require
fresh work-item reads.

## Concurrency model

The monitor uses two shared primitives:

- `AsyncWorkQueue<T>` is a bounded `Channel<T>` with fixed workers. It is used where work has a
  producer/consumer lifecycle: snapshot reads and completed-job uploads.
- `AsyncConcurrencyLimiter` shares one concurrency budget across independent calls. Azure DevOps
  work-item publication uses it so nested job pipelines never exceed
  `--test-result-upload-parallelism`.

Queue capacity is bounded, so producers receive backpressure instead of accumulating an unbounded
task list. Worker infrastructure failures are visible at drain. Expected upload failures are
handled inside the upload worker and converted to warnings.

`System.IO.Pipelines` is intentionally not used. It is optimized for byte-buffer parsing and
transport. The monitor passes owned job and work-item objects, for which channels provide clearer
backpressure, completion, cancellation, and fault semantics.

## Cancellation ownership

The caller token controls discovery, polling, and enqueueing. On normal completion the upload
channel is completed and drained. On cancellation it is abandoned: workers receive the queue stop
token and queued work is not required to finish. Helix cancellation uses a separate bounded token
because the caller token has already fired.

## State ownership

Only `MonitorLedger` mutates outcome and upload lifecycle state. The main poll loop and upload
workers may call it concurrently, so it exposes atomic operations and stable snapshots rather than
collections. It performs no I/O and emits no logs.
