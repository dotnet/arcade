// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.Maestro.Client.Models;

namespace Microsoft.DotNet.DarcLib
{
    public interface IRemote
    {
        /// <summary>
        ///     Retrieve a set of default channel associations based on the provided filters.
        /// </summary>
        /// <param name="repository">Repository name</param>
        /// <param name="branch">Name of branch</param>
        /// <param name="channel">Channel name.</param>
        /// <returns>List of default channel associations. Channel is matched based on case insensitivity.</returns>
        Task<IEnumerable<DefaultChannel>> GetDefaultChannelsAsync(
            string repository = null,
            string branch = null,
            string channel = null);

        /// <summary>
        ///     Adds a default channel association.
        /// </summary>
        /// <param name="repository">Repository receiving the default association</param>
        /// <param name="branch">Branch receiving the default association</param>
        /// <param name="channel">Name of channel that builds of 'repository' on 'branch' should automatically be applied to.</param>
        /// <returns>Async task.</returns>
        Task AddDefaultChannelAsync(string repository, string branch, string channel);

        /// <summary>
        ///     Removes a default channel based on the specified criteria
        /// </summary>
        /// <param name="repository">Repository having a default association</param>
        /// <param name="branch">Branch having a default association</param>
        /// <param name="channel">Name of channel that builds of 'repository' on 'branch' are being applied to.</param>
        /// <returns>Async task</returns>
        Task DeleteDefaultChannelAsync(string repository, string branch, string channel);

        Task<Channel> CreateChannelAsync(string name, string classification);

        Task<IEnumerable<Subscription>> GetSubscriptionsAsync(
            string sourceRepo = null,
            string targetRepo = null,
            int? channelId = null);

        Task<Subscription> GetSubscriptionAsync(string subscriptionId);

        Task<List<DependencyDetail>> GetRequiredUpdatesAsync(
            string repoUri,
            string branch,
            string sourceCommit,
            IEnumerable<AssetData> assets);

        Task CreateNewBranchAsync(string repoUri, string baseBranch, string newBranch);

        Task CommitUpdatesAsync(string repoUri, string branch, List<DependencyDetail> itemsToUpdate, string message);

        Task<PullRequest> GetPullRequestAsync(string pullRequestUri);

        Task<string> CreatePullRequestAsync(string repoUri, PullRequest pullRequest);

        Task UpdatePullRequestAsync(string pullRequestUri, PullRequest pullRequest);

        Task<Subscription> CreateSubscriptionAsync(
            string channelName,
            string sourceRepo,
            string targetRepo,
            string targetBranch,
            string updateFrequency,
            List<MergePolicy> mergePolicies);

        /// <summary>
        ///     Delete a subscription by ID.
        /// </summary>
        /// <param name="subscriptionId">Id of subscription to delete.</param>
        /// <returns>Information on deleted subscription</returns>
        Task<Subscription> DeleteSubscriptionAsync(string subscriptionId);

        Task MergePullRequestAsync(string pullRequestUrl, MergePullRequestParameters parameters);

        Task CreateOrUpdatePullRequestStatusCommentAsync(string pullRequestUrl, string message);

        Task<PrStatus> GetPullRequestStatusAsync(string pullRequestUrl);

        Task<IList<Check>> GetPullRequestChecksAsync(string pullRequestUrl);

        Task<IEnumerable<int>> SearchPullRequestsAsync(
            string repoUri,
            string pullRequestBranch,
            PrStatus status,
            string keyword = null,
            string author = null);

        Task<IList<Commit>> GetPullRequestCommitsAsync(string pullRequestUrl);
    }
}
