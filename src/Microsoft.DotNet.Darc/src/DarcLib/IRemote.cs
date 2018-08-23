// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    public interface IRemote
    {
        Task<string> CreateChannelAsync(string name, string classification, string barPassword);

        Task<string> GetSubscriptionsAsync(string barPassword, string sourceRepo = null, string targetRepo = null, int? channelId = null);

        Task<string> GetSubscriptionAsync(int subscriptionId, string barPassword);

        Task<string> CreateSubscriptionAsync(string channelName, string sourceRepo, string targetRepo, string targetBranch, string updateFrequency, string mergePolicy, string barPassword);

        Task<string> CreatePullRequestAsync(string repoUri, string branch, string assetsProducedInCommit, IEnumerable<AssetData> assets, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null);

        Task<string> UpdatePullRequestAsync(string pullRequestUrl, string assetsProducedInCommit, string branch, IEnumerable <AssetData> assetsToUpdate, string pullRequestTitle = null, string pullRequestDescription = null);

        Task MergePullRequestAsync(string pullRequestUrl, string commit = null, string mergeMethod = null, string title = null, string message = null);

        Task<PrStatus> GetPullRequestStatusAsync(string pullRequestUrl);

        Task<IList<Check>> GetPullRequestChecksAsync(string pullRequestUrl);

        Task<IEnumerable<int>> SearchPullRequestsAsync(string repoUri, string pullRequestBranch, PrStatus status, string keyword = null, string author = null);
    }
}
