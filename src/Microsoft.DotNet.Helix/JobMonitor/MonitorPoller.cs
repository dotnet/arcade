// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.JobMonitor.ResultPublishing;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.Helix.JobMonitor.Models;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    /// <summary>Acquires immutable poll snapshots with bounded, per-job work-item fan-out.</summary>
    internal sealed class MonitorPoller
    {
        private const int WorkItemReadParallelism = 8;
        private readonly JobMonitorOptions _options;
        private readonly IAzureDevOpsService _azdo;
        private readonly IHelixService _helix;
        private readonly string _source;
        private readonly Dictionary<string, IReadOnlyCollection<WorkItemSummary>> _terminalWorkItems =
            new(StringComparer.OrdinalIgnoreCase);

        public MonitorPoller(JobMonitorOptions options, IAzureDevOpsService azdo, IHelixService helix, string source)
        {
            _options = options;
            _azdo = azdo;
            _helix = helix;
            _source = source;
        }

        public async Task<MonitorSnapshot> CaptureAsync(
            IReadOnlyList<HelixJobInfo> firstPollJobs,
            CancellationToken cancellationToken)
        {
            // Timeline and Helix discovery are captured together so downstream code never mixes
            // one poll's pipeline state with another poll's Helix state.
            IReadOnlyList<AzureDevOpsTimelineRecord> timeline = HelixJobMonitorUtilities.FilterRecordsToStage(
                await _azdo.GetTimelineRecordsAsync(cancellationToken), _options.StageName);
            IReadOnlyList<HelixJobInfo> stageJobs =
            [
                ..(firstPollJobs ?? await _helix.GetJobsForBuildAsync(_source, _options.BuildId, cancellationToken))
                    .Where(IsStageInScope)
            ];

            // In-flight jobs are refreshed with bounded fan-out. Terminal work-item collections
            // are immutable for monitor purposes and remain cached to avoid repeatedly querying
            // historical jobs on long-running stages.
            var workItems = new ConcurrentDictionary<string, IReadOnlyCollection<WorkItemSummary>>(StringComparer.OrdinalIgnoreCase);
            using var reads = new AsyncWorkQueue<HelixJobInfo>(
                Math.Min(WorkItemReadParallelism, Math.Max(1, stageJobs.Count)),
                Math.Max(1, WorkItemReadParallelism * 2),
                async (job, token) => workItems[job.JobName] = await _helix.ListWorkItemsAsync(job.JobName, token));
            foreach (HelixJobInfo job in stageJobs)
            {
                if (_terminalWorkItems.TryGetValue(job.JobName, out IReadOnlyCollection<WorkItemSummary> terminalItems))
                {
                    workItems[job.JobName] = terminalItems;
                }
                else
                {
                    await reads.EnqueueAsync(job, cancellationToken);
                }
            }

            await reads.CompleteAndDrainAsync(cancellationToken);
            var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (HelixJobInfo job in stageJobs)
            {
                IReadOnlyCollection<WorkItemSummary> items = workItems[job.JobName];

                // Some failed Helix jobs never acquire a Finished timestamp. The expected-count
                // fallback is safe only once every expected work item has a terminal exit code.
                if (job.IsCompleted || (job.InitialWorkItemCount is > 0
                    && items.Count >= job.InitialWorkItemCount.Value
                    && items.All(item => item.ExitCode.HasValue)))
                {
                    completed.Add(job.JobName);
                    _terminalWorkItems[job.JobName] = items;
                }
            }

            return new MonitorSnapshot(timeline, stageJobs, workItems, completed);
        }

        public bool IsCurrentAttempt(HelixJobInfo job)
            => string.IsNullOrEmpty(_options.StageAttempt)
                || string.IsNullOrEmpty(job.StageAttempt)
                || string.Equals(job.StageAttempt, _options.StageAttempt, StringComparison.OrdinalIgnoreCase);

        public bool IsPreviousAttempt(HelixJobInfo job)
            => !string.IsNullOrEmpty(_options.StageAttempt)
                && !string.IsNullOrEmpty(job.StageAttempt)
                && MonitorLedger.ParseStageAttempt(job.StageAttempt)
                    < MonitorLedger.ParseStageAttempt(_options.StageAttempt);

        private bool IsStageInScope(HelixJobInfo job)
            => string.IsNullOrEmpty(job.StageName)
                || string.Equals(job.StageName, _options.StageName, StringComparison.OrdinalIgnoreCase);
    }
}
