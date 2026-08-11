// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.Helix.JobMonitor.Models;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    /// <summary>Executes the invocation's single, snapshot-based stage-attempt reconciliation pass.</summary>
    internal sealed class RetryPlanner
    {
        private readonly JobMonitorOptions _options;
        private readonly IAzureDevOpsService _azdo;
        private readonly IHelixService _helix;
        private readonly MonitorLedger _ledger;
        private readonly StatusReporter _reporter;
        private readonly MonitorPoller _poller;
        private readonly string _source;

        public RetryPlanner(JobMonitorOptions options, IAzureDevOpsService azdo, IHelixService helix,
            MonitorLedger ledger, StatusReporter reporter, MonitorPoller poller, string source)
        {
            _options = options;
            _azdo = azdo;
            _helix = helix;
            _ledger = ledger;
            _reporter = reporter;
            _poller = poller;
            _source = source;
        }

        public async Task<IReadOnlyList<HelixJobInfo>> ExecuteAsync(CancellationToken cancellationToken)
        {
            _reporter.LogRetryPassStart();

            // Retry is intentionally derived once from durable service state. Replanning inside
            // the poll loop could repeatedly resubmit work that fails during this invocation.
            IReadOnlyList<HelixJobInfo> stageJobs =
            [
                ..(await _helix.GetJobsForBuildAsync(_source, _options.BuildId, cancellationToken))
                    .Where(job => string.IsNullOrEmpty(job.StageName)
                        || string.Equals(job.StageName, _options.StageName, StringComparison.OrdinalIgnoreCase))
            ];
            _ledger.ObserveJobs(stageJobs);
            IReadOnlyDictionary<string, IReadOnlySet<string>> priorTestFailures = _options.FailWorkItemsWithFailedTests
                ? await _azdo.GetFailedTestWorkItemsAsync(cancellationToken)
                : new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);
            var resubmitted = new List<HelixJobInfo>();

            // Collapse stage reruns and monitor resubmissions to one latest incarnation per
            // logical submitter/queue stream before deciding whether replacement work is needed.
            foreach (HelixJobInfo latest in _ledger.GetLatestIncarnationPerStream(stageJobs))
            {
                bool previousAttempt = _poller.IsPreviousAttempt(latest);

                // Current-attempt work still has an active owner. Only abandoned previous work
                // or a completed incarnation with known failures is eligible for replacement.
                if (!previousAttempt && !latest.IsCompleted)
                {
                    continue;
                }

                IReadOnlyCollection<WorkItemSummary> workItems =
                    await _helix.ListWorkItemsAsync(latest.JobName, cancellationToken);
                priorTestFailures.TryGetValue(latest.JobName, out IReadOnlySet<string> testFailedNames);
                IReadOnlyList<WorkItemSummary> exitFailures = [.. workItems.Where(item => item.IsFailed)];
                IReadOnlyList<WorkItemSummary> testOnlyFailures =
                [
                    ..workItems.Where(item => !item.IsFailed && testFailedNames?.Contains(item.Name) == true)
                ];

                // Helix exit failures and uploaded-test failures are independent signals. Their
                // union is deduplicated because the resubmitted job list may contain each item once.
                IReadOnlyCollection<WorkItemSummary> failed =
                [
                    ..exitFailures.Concat(testOnlyFailures).DistinctBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                ];
                if (failed.Count == 0)
                {
                    continue;
                }

                _reporter.LogRetryPassResubmission(latest, exitFailures, testOnlyFailures);
                HelixJobInfo retry = await _helix.ResubmitWorkItemsAsync(latest, failed, _options.StageAttempt, cancellationToken);
                if (retry is null)
                {
                    // Previous-attempt work has no remaining owner. If it cannot be recreated,
                    // convert it to an explicit outcome failure rather than waiting forever.
                    if (previousAttempt)
                    {
                        _ledger.RecordAbandonedWork(latest, failed);
                        _reporter.LogUnresubmittablePreviousWork(latest, failed.Count);
                    }

                    continue;
                }

                resubmitted.Add(retry);
                _ledger.RecordResubmission(latest.SubmitterJobName, failed.Count);
            }

            if (resubmitted.Count == 0)
            {
                _reporter.LogRetryPassFoundNothing();
            }

            return [.. stageJobs, .. resubmitted];
        }
    }
}
