// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.Helix.JobMonitor.Models;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    /// <summary>
    /// Abstracts Helix API interactions needed by the job monitor.
    /// </summary>
    public interface IHelixService
    {
        /// <summary>
        /// Returns Helix jobs associated with the current build/stage.
        /// </summary>
        Task<IReadOnlyList<HelixJobInfo>> GetJobsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Downloads test result files for a completed Helix job's work items
        /// and returns metadata about each work item's results.
        /// </summary>
        Task<IReadOnlyList<WorkItemTestResults>> DownloadTestResultsAsync(
            string jobName,
            IReadOnlyCollection<string> workItemNames,
            CancellationToken cancellationToken);

        Task<IReadOnlyCollection<WorkItemSummary>> ListWorkItemsAsync(
            string jobName,
            CancellationToken cancellationToken);

        /// <summary>
        /// Resubmits the specified failed work items from a completed Helix job as a new job.
        /// The new job copies correlation payloads and queue from the original, but only includes
        /// the specified work items. Returns the new job's info, or null if resubmission is not possible.
        /// The new job must preserve BuildId and StageName properties so it is discoverable by GetJobsAsync.
        /// </summary>
        Task<HelixJobInfo> ResubmitWorkItemsAsync(
            string originalJobName,
            IReadOnlyCollection<string> failedWorkItemNames,
            CancellationToken cancellationToken);
    }
}
