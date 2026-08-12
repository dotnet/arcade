# Upload pipeline

`TestResultUploadPipeline` contains three worker stages. The lightweight job
stage is unbounded so completed jobs cannot be dropped and upload backpressure
cannot block polling. The expensive work-item and finalization stages are
bounded.

## Job expansion

A completed Helix job is accepted once. A `JobUploadSession` captures the
completion-time work-item snapshot and owns:

- a single-flight Azure DevOps test-run creation task;
- expected and finished work-item counts;
- failed-test work-item names;
- uploaded-result count;
- a sticky failure flag.

The job stage expands work items into the global work-item queue. A job with no
work items proceeds directly to finalization.

## Work-item processing

Each worker:

1. downloads recognized result files for one work item, retrying only
   transient read failures;
2. obtains the session's single test-run ID;
3. parses, aggregates, batches, and publishes results;
4. records the upload summary and test-only failure outcome;
5. signals session completion.

Work-item concurrency is global. A build with many jobs therefore cannot create
an unbounded task graph or multiply the configured Azure DevOps pressure.

## Finalization

The last work item queues its session for finalization. If any work item
failed, the session remains untagged. Otherwise finalization uploads the
failed-work-item attachment, marks the run completed, applies the Helix-job
tag, and only then marks the job durably processed.

Create and complete are not replayed after ambiguous failures. Result and
attachment publication use bounded transient retries because losing an entire
job's results is worse than the accepted duplicate risk.
