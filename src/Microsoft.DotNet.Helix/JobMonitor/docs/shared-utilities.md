# Shared utilities

## AsyncWorkQueue<T>

The queue owns a bounded `Channel<T>`, fixed workers, and a private stop token.

- `EnqueueAsync` waits for capacity and therefore provides producer backpressure.
- `CompleteAndDrainAsync` closes input, waits for accepted work, and surfaces worker faults.
- `Abandon` closes input and cancels workers without draining.
- Operational failures that are intentionally non-fatal must be handled by the work handler.

It is used for job uploads, per-poll work-item reads, and other finite object pipelines.

## AsyncConcurrencyLimiter

The limiter owns a semaphore as an implementation detail and exposes `RunAsync`. Unlike a
per-call queue, one limiter instance shares a concurrency budget across multiple job uploads.
Azure DevOps work-item publication uses it to enforce the user-facing maximum globally.

## RetryExecutor

The executor wraps `Microsoft.Arcade.Common.ExponentialRetry` and
`TransientFailureDetector`. It retries only known transient failures, uses deterministic bounded
backoff, propagates caller cancellation, and immediately propagates permanent failures.

Call sites decide whether an operation is safe to retry. Reads and idempotent service operations
use the executor. Test-run creation, publication, and completion are intentionally one-shot when
an ambiguous response could duplicate or prematurely complete durable state.
