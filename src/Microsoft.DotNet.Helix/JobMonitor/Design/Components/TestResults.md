# Test-result processing

Result processing lives in the shared `Microsoft.DotNet.Helix.TestResults`
library. Both the Helix SDK and Job Monitor use its readers, aggregation,
downloader, and Azure DevOps publisher. Submission, polling, test-run lifecycle,
and failure policies remain caller responsibilities.

## Parsing

`LocalTestResultsReader` uses `XmlReader` over a `FileStream`. xUnit and JUnit
records are materialized one result element at a time. TRX uses a small first
pass for test definitions followed by a result pass, because TRX result records
refer to definitions by ID and may precede them in the file.

Malformed files are warned and omitted. Caller cancellation is propagated.
DTD processing is prohibited.

The SDK's inline caller enables strict parsing: malformed files and unsupported
roots fail reporting rather than allowing a successful work-item result to hide
missing tests. The monitor retains its warning-and-omission parsing policy.

Both callers use the shared downloader and transport. Download failures are
reported after attempting the other files; partial downloads and incomplete
Azure DevOps result responses do not count as successful publication. The
monitor leaves failed uploads untagged for replay.

Helix may append `.txt` to uploaded result artifacts, for example
`testResults.xml.txt` or `results.trx.txt`. File recognition strips that single
transport suffix before matching the supported xUnit, JUnit, and TRX names.
Recognized files whose XML root is unsupported are warned rather than silently
producing zero results.

## Aggregation

Existing single, data-driven, and rerun semantics are retained, including
flaky-result fields, fully qualified identity, attachment selection, and the
rule that `Inconclusive` does not fail a work item.

Aggregation state is scoped to one work item. This bounds normal memory by the
largest concurrently processed work items rather than the whole Helix job or
build. A single pathological work item with millions of distinct tests still
sets the memory floor; handling that case would require spill-to-disk grouping.

## Azure DevOps batching

- A request contains at most 1,000 top-level results.
- Nested sub-results do not consume that request limit.
- A single oversized hierarchy is defensively split below 950 recursive nodes.
- Converted results are enumerated lazily into one request batch at a time.
- The serialized UTF-8 request body is retained only for the lifetime of that
  request and its retries.
- Azure DevOps rate-limit guidance is applied through a service-wide gate so
  concurrent workers slow down together instead of stampeding a throttled
  endpoint independently. A response advances the gate for future requests;
  the request that already received the response returns immediately rather
  than redundantly adding the advertised delay to its own completion time.
