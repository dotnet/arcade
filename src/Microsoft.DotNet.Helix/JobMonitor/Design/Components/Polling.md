# Polling and reconciliation

Each poll concurrently refreshes work-item summaries for in-scope jobs that
have not yet been reconciled, with a fixed degree of parallelism. Terminal
snapshots are retained for later status reports instead of being downloaded
again. The resulting dictionary is the poll's immutable work-item snapshot and
is reused for:

- completion fallback when a Helix job summary has not yet transitioned;
- outcome reconciliation;
- upload scheduling;
- failed-work-item links;
- aggregate status counts.

No second service call is made for status.

The one-shot entry retry pass and stage-attempt semantics are specified in
[the semantic document](../SemanticBehavior.md). Outcome updates are
applied oldest-to-newest so resubmissions and higher stage attempts supersede
older failures without allowing identically named work from different
submitter/queue/logical-job streams to collide.
