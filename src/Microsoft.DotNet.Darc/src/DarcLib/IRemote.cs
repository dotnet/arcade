// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    public interface IRemote
    {
        Task<string> CreateChannelAsync(string name, string classification);

        Task<string> GetSubscriptionsAsync(string sourceRepo = null, string targetRepo = null, int? channelId = null);

        Task<string> GetSubscriptionAsync(int subscriptionId);

        Task<string> CreateSubscriptionAsync(string channelName, string sourceRepo, string targetRepo, string targetBranch, string updateFrequency, string mergePolicy);

        Task<IEnumerable<DependencyDetail>> GetRequiredUpdatesAsync(string repoUri, string branch);

        Task<string> CreatePullRequestAsync(IEnumerable<DependencyDetail> itemsToUpdate, string repoUri, string branch, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null);

        Task<string> UpdatePullRequestAsync(IEnumerable<DependencyDetail> itemsToUpdate, string repoUri, string branch, int pullRequestId, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null);
    }
}
