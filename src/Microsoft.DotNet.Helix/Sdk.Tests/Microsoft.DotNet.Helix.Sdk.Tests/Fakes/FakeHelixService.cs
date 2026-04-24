// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.JobMonitor;
using Microsoft.DotNet.Helix.JobMonitor.Models;

namespace Microsoft.DotNet.Helix.Sdk.Tests.Fakes
{
    internal sealed class FakeHelixService : IHelixService
    {
        private readonly List<HelixSnapshot> _snapshots = [];
        private readonly HashSet<string> _downloadFailureJobs = new(StringComparer.OrdinalIgnoreCase);
        private int _currentSnapshotIndex;

        public FakeHelixService AddSnapshot(
            HelixJobInfo[] jobs,
            Dictionary<string, HelixJobPassFail> passFailByJob = null,
            Dictionary<string, List<WorkItemTestResults>> testResultsByJob = null)
        {
            _snapshots.Add(new HelixSnapshot(
                jobs,
                passFailByJob ?? new Dictionary<string, HelixJobPassFail>(StringComparer.OrdinalIgnoreCase),
                testResultsByJob ?? new Dictionary<string, List<WorkItemTestResults>>(StringComparer.OrdinalIgnoreCase)));
            return this;
        }

        public FakeHelixService FailDownloadForJob(string jobName) { _downloadFailureJobs.Add(jobName); return this; }
        public void ClearDownloadFailures() { _downloadFailureJobs.Clear(); }

        public void AdvanceSnapshot()
        {
            if (_currentSnapshotIndex < _snapshots.Count - 1) _currentSnapshotIndex++;
        }

        private HelixSnapshot CurrentSnapshot => _snapshots[Math.Min(_currentSnapshotIndex, _snapshots.Count - 1)];

        public Task<IReadOnlyList<HelixJobInfo>> GetJobsAsync(CancellationToken cancellationToken)
        {
            if (_snapshots.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<HelixJobInfo>>(Array.Empty<HelixJobInfo>());
            }

            return Task.FromResult<IReadOnlyList<HelixJobInfo>>(CurrentSnapshot.Jobs);
        }

        public Task<HelixJobPassFail> GetJobPassFailAsync(string jobName, CancellationToken cancellationToken)
        {
            if (CurrentSnapshot.PassFailByJob.TryGetValue(jobName, out HelixJobPassFail passFail))
            {
                return Task.FromResult(passFail);
            }

            throw new InvalidOperationException($"No pass/fail data was configured for Helix job '{jobName}'.");
        }

        public Task<IReadOnlyList<WorkItemTestResults>> DownloadTestResultsAsync(
            string jobName, HelixJobPassFail passFail, CancellationToken cancellationToken)
        {
            if (_downloadFailureJobs.Contains(jobName))
            {
                throw new InvalidOperationException($"Injected download failure for Helix job '{jobName}'.");
            }

            if (CurrentSnapshot.TestResultsByJob.TryGetValue(jobName, out List<WorkItemTestResults> explicitResults))
            {
                return Task.FromResult<IReadOnlyList<WorkItemTestResults>>(explicitResults);
            }

            List<string> workItemNames = passFail.PassedWorkItems
                .Concat(passFail.FailedWorkItems)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .DefaultIfEmpty($"{jobName}-synthetic")
                .ToList();

            IReadOnlyList<WorkItemTestResults> generated = workItemNames
                .Select(wi => new WorkItemTestResults(jobName, wi, []))
                .ToList();

            return Task.FromResult(generated);
        }

        private sealed record HelixSnapshot(
            HelixJobInfo[] Jobs,
            Dictionary<string, HelixJobPassFail> PassFailByJob,
            Dictionary<string, List<WorkItemTestResults>> TestResultsByJob);
    }
}
