// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.JobMonitor;

namespace Microsoft.DotNet.Helix.Sdk.Tests.Fakes
{
    internal sealed class FakeAzureDevOpsService : IAzureDevOpsService
    {
        private readonly List<AzureDevOpsTimelineRecord[]> _timelineSnapshots = [];
        private readonly HashSet<string> _previouslyProcessedJobs = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _inProgressRunsByJobName = new(StringComparer.OrdinalIgnoreCase);
        private int _currentTimelineIndex;
        private int _nextTestRunId;

        // Observable state for test assertions
        public List<string> CreatedTestRuns { get; } = [];
        public List<int> CompletedTestRunIds { get; } = [];
        public Dictionary<int, List<WorkItemTestResults>> UploadedResultsByRunId { get; } = [];
        public List<string> UploadedJobNames { get; } = [];
        public int CreateTestRunCallCount { get; private set; }

        // Configuration
        public FakeAzureDevOpsService AddTimelineSnapshot(AzureDevOpsTimelineRecord[] records)
        {
            _timelineSnapshots.Add(records);
            return this;
        }

        public FakeAzureDevOpsService WithPreviouslyProcessedJob(string jobName)
        {
            _previouslyProcessedJobs.Add(jobName);
            return this;
        }

        public void AdvanceTimeline()
        {
            if (_currentTimelineIndex < _timelineSnapshots.Count - 1)
            {
                _currentTimelineIndex++;
            }
        }

        // IAzureDevOpsService implementation
        public Task<IReadOnlyList<AzureDevOpsTimelineRecord>> GetTimelineRecordsAsync(CancellationToken cancellationToken)
        {
            if (_timelineSnapshots.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<AzureDevOpsTimelineRecord>>(Array.Empty<AzureDevOpsTimelineRecord>());
            }

            AzureDevOpsTimelineRecord[] snapshot = _timelineSnapshots[Math.Min(_currentTimelineIndex, _timelineSnapshots.Count - 1)];
            return Task.FromResult<IReadOnlyList<AzureDevOpsTimelineRecord>>(snapshot);
        }

        public Task<IReadOnlySet<string>> GetProcessedHelixJobNamesAsync(CancellationToken cancellationToken)
        {
            var result = new HashSet<string>(_previouslyProcessedJobs, StringComparer.OrdinalIgnoreCase);
            return Task.FromResult<IReadOnlySet<string>>(result);
        }

        public Task<int> CreateTestRunAsync(string name, string helixJobName, CancellationToken cancellationToken)
        {
            CreateTestRunCallCount++;

            // Idempotent: if a run for this helix job is in-progress, reuse it
            if (_inProgressRunsByJobName.TryGetValue(helixJobName, out int existingId))
            {
                return Task.FromResult(existingId);
            }

            int id = Interlocked.Increment(ref _nextTestRunId);
            CreatedTestRuns.Add(name);
            _inProgressRunsByJobName[helixJobName] = id;
            return Task.FromResult(id);
        }

        public Task CompleteTestRunAsync(int testRunId, CancellationToken cancellationToken)
        {
            CompletedTestRunIds.Add(testRunId);

            string keyToRemove = null;
            foreach (var kvp in _inProgressRunsByJobName)
            {
                if (kvp.Value == testRunId) { keyToRemove = kvp.Key; break; }
            }

            if (keyToRemove != null) _inProgressRunsByJobName.Remove(keyToRemove);
            return Task.CompletedTask;
        }

        public Task<bool> UploadTestResultsAsync(int testRunId, IReadOnlyList<WorkItemTestResults> results, CancellationToken cancellationToken)
        {
            if (!UploadedResultsByRunId.TryGetValue(testRunId, out List<WorkItemTestResults> existing))
            {
                existing = [];
                UploadedResultsByRunId[testRunId] = existing;
            }

            existing.AddRange(results);

            foreach (string jobName in results.Select(r => r.JobName).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                UploadedJobNames.Add(jobName);
                _previouslyProcessedJobs.Add(jobName);
            }

            return Task.FromResult(true);
        }
    }
}
