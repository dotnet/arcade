// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class SetupTargetFeedConfigV3 : SetupTargetFeedConfigBase
    {
        private readonly List<TargetFeedContentType> Installers = new List<TargetFeedContentType>() {
            TargetFeedContentType.OSX,
            TargetFeedContentType.Deb,
            TargetFeedContentType.Rpm,
            TargetFeedContentType.Node,
            TargetFeedContentType.BinaryLayout,
            TargetFeedContentType.Installer,
            TargetFeedContentType.Maven,
            TargetFeedContentType.VSIX,
            TargetFeedContentType.Badge,
            TargetFeedContentType.Other
        };

        private IBuildEngine BuildEngine { get; }
        
        private string StablePackagesFeed { get; set; }
        
        private string StableSymbolsFeed { get; set; }

        public SetupTargetFeedConfigV3(bool isInternalBuild,
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
            string azureDevOpsFeedsKey,
            IBuildEngine buildEngine,
            string stablePackagesFeed = null,
            string stableSymbolsFeed = null) 
            : base(isInternalBuild, isStableBuild, repositoryName, commitSha, azureStorageTargetFeedPAT, publishInstallersAndChecksums, installersTargetStaticFeed, installersAzureAccountKey, checksumsTargetStaticFeed, checksumsAzureAccountKey, azureDevOpsStaticShippingFeed, azureDevOpsStaticTransportFeed, azureDevOpsStaticSymbolsFeed, latestLinkShortUrlPrefix, azureDevOpsFeedsKey)
        {
            BuildEngine = buildEngine;
            StableSymbolsFeed = stableSymbolsFeed;
            StablePackagesFeed = stablePackagesFeed;
        }

        public override List<TargetFeedConfig> Setup()
        {
            if (string.IsNullOrEmpty(InstallersAzureAccountKey))
            {
                throw new ArgumentException("Parameters 'InstallersAzureAccountKey' is empty.");
            }

            if (string.IsNullOrEmpty(ChecksumsAzureAccountKey))
            {
                throw new ArgumentException("Parameters 'ChecksumsAzureAccountKey' is empty.");
            }

            if (IsStableBuild)
            {
                return StableFeeds();
            }
            else
            {
                if (IsInternalBuild)
                {
                    return NonStableInternalFeeds();
                }
                else
                {
                    return NonStablePublicFeeds();
                }
            }
        }

        private List<TargetFeedConfig> NonStablePublicFeeds()
        {
            List<TargetFeedConfig> targetFeedConfigs = new List<TargetFeedConfig>();

            if (PublishInstallersAndChecksums)
            {
                targetFeedConfigs.Add(
                    new TargetFeedConfig(
                        TargetFeedContentType.Checksum,
                        ChecksumsTargetStaticFeed,
                        FeedType.AzureStorageFeed,
                        ChecksumsAzureAccountKey,
                        LatestLinkShortUrlPrefix));

                foreach (var contentType in Installers)
                {
                    targetFeedConfigs.Add(
                        new TargetFeedConfig(
                            contentType,
                            InstallersTargetStaticFeed,
                            FeedType.AzureStorageFeed,
                            InstallersAzureAccountKey,
                            LatestLinkShortUrlPrefix));
                }
            }

            targetFeedConfigs.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Symbols,
                    PublishingConstants.LegacyDotNetBlobFeedURL,
                    FeedType.AzureStorageFeed,
                    AzureStorageTargetFeedPAT));

            targetFeedConfigs.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    AzureDevOpsStaticShippingFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    assetSelection: AssetSelection.ShippingOnly));

            targetFeedConfigs.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    AzureDevOpsStaticTransportFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    assetSelection: AssetSelection.NonShippingOnly));

            return targetFeedConfigs;
        }

        private List<TargetFeedConfig>  NonStableInternalFeeds()
        {
            List<TargetFeedConfig> targetFeedConfigs = new List<TargetFeedConfig>
            {
                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    AzureDevOpsStaticShippingFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    assetSelection: AssetSelection.ShippingOnly,
                    @internal: true
                ),

                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    AzureDevOpsStaticTransportFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    assetSelection: AssetSelection.NonShippingOnly,
                    @internal: true
                ),

                new TargetFeedConfig(
                    TargetFeedContentType.Symbols,
                    AzureDevOpsStaticSymbolsFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    @internal: true)
            };

            if (PublishInstallersAndChecksums)
            {
                foreach (var contentType in Installers)
                {
                    targetFeedConfigs.Add(
                        new TargetFeedConfig(
                            contentType,
                            InstallersTargetStaticFeed,
                            FeedType.AzureStorageFeed,
                            InstallersAzureAccountKey,
                            @internal: true
                        ));
                }

                targetFeedConfigs.Add(
                    new TargetFeedConfig(
                        TargetFeedContentType.Checksum,
                        ChecksumsTargetStaticFeed,
                        FeedType.AzureStorageFeed,
                        ChecksumsAzureAccountKey,
                        @internal: true
                    ));
            }

            return targetFeedConfigs;
        }

        private List<TargetFeedConfig> StableFeeds()
        {
            List<TargetFeedConfig> targetFeedConfigs = new List<TargetFeedConfig>();

            if (string.IsNullOrEmpty(StablePackagesFeed))
            {
                var packagesFeedTask = new CreateAzureDevOpsFeed()
                {
                    BuildEngine = BuildEngine,
                    IsInternal = IsInternalBuild,
                    AzureDevOpsPersonalAccessToken = AzureDevOpsFeedsKey,
                    RepositoryName = RepositoryName,
                    CommitSha = CommitSha
                };

                if (packagesFeedTask.Execute())
                {
                    throw new Exception($"Problems creating an AzureDevOps feed for repository '{RepositoryName}' and commit '{CommitSha}'.");
                }

                StablePackagesFeed = packagesFeedTask.TargetFeedURL;
            }

            if (string.IsNullOrEmpty(StableSymbolsFeed))
            {
                var symbolsFeedTask = new CreateAzureDevOpsFeed()
                {
                    BuildEngine = BuildEngine,
                    IsInternal = IsInternalBuild,
                    AzureDevOpsPersonalAccessToken = AzureDevOpsFeedsKey,
                    RepositoryName = RepositoryName,
                    CommitSha = CommitSha,
                    ContentIdentifier = "sym"
                };

                if (!symbolsFeedTask.Execute())
                {
                    throw new Exception($"Problems creating an AzureDevOps (symbols) feed for repository '{RepositoryName}' and commit '{CommitSha}'.");
                }

                StableSymbolsFeed = symbolsFeedTask.TargetFeedURL;
            }

            targetFeedConfigs.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    StablePackagesFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    assetSelection: AssetSelection.ShippingOnly,
                    isolated: true));

            targetFeedConfigs.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Symbols,
                    StableSymbolsFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    isolated: true));

            targetFeedConfigs.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    AzureDevOpsStaticTransportFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    assetSelection: AssetSelection.NonShippingOnly,
                    isolated: false));

            if (PublishInstallersAndChecksums)
            {
                foreach (var contentType in Installers)
                {
                    targetFeedConfigs.Add(
                        new TargetFeedConfig(
                            contentType,
                            InstallersTargetStaticFeed,
                            FeedType.AzureStorageFeed,
                            InstallersAzureAccountKey,
                            isolated: true,
                            @internal: false,
                            allowOverwrite: true));
                }

                targetFeedConfigs.Add(
                    new TargetFeedConfig(
                        TargetFeedContentType.Checksum,
                        ChecksumsTargetStaticFeed,
                        FeedType.AzureStorageFeed,
                        ChecksumsAzureAccountKey,
                        isolated: true,
                        @internal: false,
                        allowOverwrite: true));
            }

            return targetFeedConfigs;
        }
    }
}
