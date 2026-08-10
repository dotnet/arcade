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
        /// Implementations should query Helix using the given <paramref name="source"/>
        /// filter (which scopes the query to the repo/branch/PR the build is for, mirroring
        /// what the Helix job submitter records on each submission) and then narrow the
        /// result to jobs stamped with <paramref name="buildId"/>.
        /// </summary>
        Task<IReadOnlyList<HelixJobInfo>> GetJobsForBuildAsync(
            string source,
            string buildId,
            CancellationToken cancellationToken);

        /// <summary>
        /// Resolves the job-scoped output directory and results SAS used to retrieve test results.
        /// The returned context can be reused to download individual work items without resolving
        /// the job results endpoint again.
        /// </summary>
        Task<HelixTestResultsContext> CreateTestResultsContextAsync(
            string jobName,
            string workingDirectory,
            CancellationToken cancellationToken);

        /// <summary>
        /// Lists and downloads recognizable test result files for one work item using a
        /// previously resolved job context. Individual file download failures should not
        /// prevent the remaining files from being attempted.
        /// </summary>
        Task<WorkItemTestResults> DownloadTestResultsAsync(
            HelixTestResultsContext context,
            string workItemName,
            CancellationToken cancellationToken);

        /// <summary>
        /// Lists work items for the specified Helix job.
        /// </summary>
        Task<IReadOnlyCollection<WorkItemSummary>> ListWorkItemsAsync(
            string jobName,
            CancellationToken cancellationToken);

        /// <summary>
        /// Requests cancellation for the specified Helix job.
        /// </summary>
        Task CancelJobAsync(
            string jobName,
            CancellationToken cancellationToken);

        /// <summary>
        /// Resubmits the specified failed or unfinished work items from a Helix job as a new job.
        /// The new job copies correlation payloads and queue from the original, but only includes
        /// the specified work items. Returns the new job's info, or null if resubmission is not
        /// possible (e.g. the original queue no longer exists).
        /// The new job must preserve BuildId and StageName properties so it is discoverable by
        /// GetJobsForBuildAsync, and must be stamped with <paramref name="targetStageAttempt"/>
        /// (the resubmitting monitor's own stage attempt) rather than the original job's attempt,
        /// so the monitor gates on its own resubmission. When <paramref name="targetStageAttempt"/>
        /// is null/empty the original job's attempt is preserved (build + stage back-compat).
        /// </summary>
        Task<HelixJobInfo> ResubmitWorkItemsAsync(
            HelixJobInfo originalJob,
            IReadOnlyCollection<WorkItemSummary> failedWorkItems,
            string targetStageAttempt,
            CancellationToken cancellationToken);
    }
}
