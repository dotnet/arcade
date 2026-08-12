# State and status

`MonitorState` owns invocation state behind one lock. Collections are exposed
only through immutable snapshots. Upload workers update only narrow methods:
upload lifecycle, uploaded test outcomes, and durable completion.

The reporter consumes the current poll snapshot and atomic upload-pipeline
counters. It never waits for Helix file access or Azure DevOps result uploads.

Normal logging reports semantic transitions and aggregate counts. Verbose
logging adds queue depth, active worker counts, finalizer depth, and uploaded
result totals. It deliberately does not print every job, work item, file, or
request; verbose mode must remain usable on runs containing thousands of work
items and millions of results.

