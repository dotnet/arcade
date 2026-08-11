# MonitorPoller

`MonitorPoller` acquires the immutable observation used by one loop iteration.

Timeline and job discovery are stage scoped. Work-item listings are fetched through a bounded
`AsyncWorkQueue<HelixJobInfo>` with fixed fan-out, and each job is listed exactly once. The
resulting dictionary is reused by completion detection, outcome reconciliation, and reporting.
Terminal work-item listings are cached for the rest of the invocation, while in-flight jobs are
refreshed each poll. This keeps active state current without repeatedly querying historical jobs.

A job is complete when Helix reports a terminal job status. The fallback requires a known,
positive initial work-item count, at least that many observed work items, and an exit code on
every observed item.

The poller also owns attempt classification:

- unknown monitor or job attempts are treated as current for compatibility;
- equal attempts are current;
- only a numerically lower job attempt is previous.

It performs no state mutation and no logging.
