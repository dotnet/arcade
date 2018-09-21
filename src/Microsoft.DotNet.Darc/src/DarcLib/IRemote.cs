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
        Task<Channel> CreateChannelAsync(string name, string classification);

        Task<IEnumerable<Subscription>> GetSubscriptionsAsync(string sourceRepo = null, string targetRepo = null, int? channelId = null);

        Task<Subscription> GetSubscriptionAsync(string subscriptionId);

        Task<Subscription> CreateSubscriptionAsync(string channelName, string sourceRepo, string targetRepo, string targetBranch, string updateFrequency, string mergePolicy);

        Task<string> CreatePullRequestAsync(string repoUri, string branch, string assetsProducedInCommit, IEnumerable<Microsoft.DotNet.DarcLib.AssetData> assets, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null);

        Task<string> UpdatePullRequestAsync(string pullRequestUrl, string assetsProducedInCommit, string branch, IEnumerable <Microsoft.DotNet.DarcLib.AssetData> assetsToUpdate, string pullRequestTitle = null, string pullRequestDescription = null);

        Task MergePullRequestAsync(string pullRequestUrl, MergePullRequestParameters parameters);

        Task<string> CreatePullRequestCommentAsync(string pullRequestUrl, string message);

        Task UpdatePullRequestCommentAsync(string pullRequestUrl, string commentId, string message);

        Task<PrStatus> GetPullRequestStatusAsync(string pullRequestUrl);

        Task<IList<Check>> GetPullRequestChecksAsync(string pullRequestUrl);

        Task<IEnumerable<int>> SearchPullRequestsAsync(string repoUri, string pullRequestBranch, PrStatus status, string keyword = null, string author = null);

        Task<IList<Commit>> GetPullRequestCommitsAsync(string pullRequestUrl);
    }
}
