// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Build.Tasks.Feed.Model;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public abstract class SetupTargetFeedConfigBase
    {
        protected bool IsInternalBuild { get; set; }
        protected bool IsStableBuild { get; set; }
        protected string RepositoryName { get; set; }
        protected string CommitSha { get; set; }
        protected string AzureStorageTargetFeedPAT { get; set; }
        protected bool PublishInstallersAndChecksums { get; set; }
        protected string InstallersTargetStaticFeed { get; set; }
        protected string InstallersAzureAccountKey { get; set; }
        protected string ChecksumsTargetStaticFeed { get; set; }
        protected string ChecksumsAzureAccountKey { get; set; }
        protected string AzureDevOpsStaticShippingFeed { get; set; }
        protected string AzureDevOpsStaticTransportFeed { get; set; }
        protected string AzureDevOpsStaticSymbolsFeed { get; set; }
        protected string LatestLinkShortUrlPrefix { get; set; }
        protected string AzureDevOpsFeedsKey { get; set; }

        protected SetupTargetFeedConfigBase(bool isInternalBuild,
            bool isStableBuild,
            string repositoryName,
            string commitSha,
            string azureStorageTargetFeedPAT,
            bool publishInstallersAndChecksums,
            string installersTargetStaticFeed,
            string installersAzureAccountKey,
            string checksumsTargetStaticFeed,
            string checksumsAzureAccountKey,
            string azureDevOpsStaticShippingFeed,
            string azureDevOpsStaticTransportFeed,
            string azureDevOpsStaticSymbolsFeed,
            string latestLinkShortUrlPrefix,
            string azureDevOpsFeedsKey)
        {
            IsInternalBuild = isInternalBuild;
            IsStableBuild = isStableBuild;
            RepositoryName = repositoryName;
            CommitSha = commitSha;
            AzureStorageTargetFeedPAT = azureStorageTargetFeedPAT;
            PublishInstallersAndChecksums = publishInstallersAndChecksums;
            InstallersTargetStaticFeed = installersTargetStaticFeed;
            InstallersAzureAccountKey = installersAzureAccountKey;
            ChecksumsTargetStaticFeed = checksumsTargetStaticFeed;
            ChecksumsAzureAccountKey = checksumsAzureAccountKey;
            AzureDevOpsStaticShippingFeed = azureDevOpsStaticShippingFeed;
            AzureDevOpsStaticTransportFeed = azureDevOpsStaticTransportFeed;
            AzureDevOpsStaticSymbolsFeed = azureDevOpsStaticSymbolsFeed;
            LatestLinkShortUrlPrefix = latestLinkShortUrlPrefix;
            AzureDevOpsFeedsKey = azureDevOpsFeedsKey;
        }
        public abstract List<TargetFeedConfig> Setup();
    }
}
