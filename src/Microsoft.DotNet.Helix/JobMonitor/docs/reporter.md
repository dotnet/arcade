# StatusReporter

The reporter is rendering-only. It never queries Helix or Azure DevOps.

Poll status is computed from a `MonitorSnapshot`, guaranteeing that aggregate counts, verbose
trees, completion state, and failure links describe the same observation. Warning links are
deduplicated through the ledger.

The reporter owns:

- monitor and retry-pass announcements;
- resubmission details grouped by exit-code and test-only cause;
- job completion summaries and console links;
- periodic aggregate and verbose status;
- timeout diagnostics for unfinished Helix and Azure DevOps jobs;
- final work-item failure trees and summary counts.

Azure DevOps warning and error decorations are part of the external contract. In particular, the
non-monitor pipeline failure and final failed-work-item lines are matched by Build Analysis and
must remain stable.
