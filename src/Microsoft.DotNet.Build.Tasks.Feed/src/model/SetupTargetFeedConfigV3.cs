// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Feed.Model;

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

        private string AzureDevOpsPublicStaticSymbolsFeed { get; set; }

        private SymbolTargetType SymbolTargetType { get; set; }

        private List<string> FilesToExclude { get; }

        private bool Flatten { get; }

        public SetupTargetFeedConfigV3(bool isInternalBuild,
            bool isStableBuild,
            string repositoryName,
            string commitSha,
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
            SymbolTargetType symbolTargetType,
            string stablePackagesFeed = null,
            string stableSymbolsFeed = null,
            string azureDevOpsPublicStaticSymbolsFeed = null,
            List<string> filesToExclude = null,
            bool flatten = true) 
            : base(isInternalBuild, isStableBuild, repositoryName, commitSha, publishInstallersAndChecksums, installersTargetStaticFeed, installersAzureAccountKey, checksumsTargetStaticFeed, checksumsAzureAccountKey, azureDevOpsStaticShippingFeed, azureDevOpsStaticTransportFeed, azureDevOpsStaticSymbolsFeed, latestLinkShortUrlPrefix, azureDevOpsFeedsKey)
        {
            BuildEngine = buildEngine;
            StableSymbolsFeed = stableSymbolsFeed;
            StablePackagesFeed = stablePackagesFeed;
            SymbolTargetType = symbolTargetType;
            AzureDevOpsPublicStaticSymbolsFeed = azureDevOpsPublicStaticSymbolsFeed;
            FilesToExclude = filesToExclude ?? new List<string>();
            Flatten = flatten;
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
                return NonStableFeeds();
            }
        }

        private List<TargetFeedConfig> NonStableFeeds()
        {
            List<TargetFeedConfig> targetFeedConfigs = new List<TargetFeedConfig>();

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
                            latestLinkShortUrlPrefix: LatestLinkShortUrlPrefix,
                            @internal: IsInternalBuild,
                            symbolTargetType: SymbolTargetType,
                            filenamesToExclude: FilesToExclude,
                            flatten: Flatten));
                }

                targetFeedConfigs.Add(
                    new TargetFeedConfig(
                        TargetFeedContentType.Checksum,
                        ChecksumsTargetStaticFeed,
                        FeedType.AzureStorageFeed,
                        ChecksumsAzureAccountKey,
                        latestLinkShortUrlPrefix: LatestLinkShortUrlPrefix,
                        @internal: IsInternalBuild,
                        symbolTargetType: SymbolTargetType,
                        filenamesToExclude: FilesToExclude,
                        flatten: Flatten));
            }

            targetFeedConfigs.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    AzureDevOpsStaticShippingFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    assetSelection: AssetSelection.ShippingOnly,
                    @internal: IsInternalBuild,
                    symbolTargetType: SymbolTargetType,
                    filenamesToExclude: FilesToExclude,
                    flatten: Flatten));

            targetFeedConfigs.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    AzureDevOpsStaticTransportFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    assetSelection: AssetSelection.NonShippingOnly,
                    @internal: IsInternalBuild,
                    symbolTargetType: SymbolTargetType,
                    filenamesToExclude: FilesToExclude,
                    flatten: Flatten));

            // For symbols, we don't have a blob location where internal symbols can go today,
            // so a feed is used in this case. This would be a potential performance improvement for internal builds.
            // This is pretty uncommon though, as non-stable internal builds are quite rare.
            string symbolsFeed;
            FeedType symbolsFeedType;
            string symbolsFeedSecret;

            if (IsInternalBuild)
            {
                symbolsFeed = AzureDevOpsStaticSymbolsFeed;
                symbolsFeedType = FeedType.AzDoNugetFeed;
                symbolsFeedSecret = AzureDevOpsFeedsKey;
            }
            else if (!string.IsNullOrEmpty(AzureDevOpsPublicStaticSymbolsFeed))
            {
                symbolsFeed = AzureDevOpsPublicStaticSymbolsFeed;
                symbolsFeedType = FeedType.AzDoNugetFeed;
                symbolsFeedSecret = AzureDevOpsFeedsKey;
            }
            else
            {
                symbolsFeed = PublishingConstants.LegacyDotNetBlobFeedURL;
                symbolsFeedType = FeedType.AzureStorageFeed;
            }

            targetFeedConfigs.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Symbols,
                    symbolsFeed,
                    symbolsFeedType,
                    symbolsFeedSecret,
                    symbolTargetType: SymbolTargetType,
                    @internal: IsInternalBuild,
                    filenamesToExclude: FilesToExclude,
                    flatten: Flatten));

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

                if (!packagesFeedTask.Execute())
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
                    symbolTargetType: SymbolTargetType,
                    isolated: true,
                    filenamesToExclude: FilesToExclude,
                    flatten: Flatten));

            targetFeedConfigs.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Symbols,
                    StableSymbolsFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    symbolTargetType: SymbolTargetType,
                    isolated: true,
                    filenamesToExclude: FilesToExclude,
                    flatten: Flatten));

            targetFeedConfigs.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    AzureDevOpsStaticTransportFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    assetSelection: AssetSelection.NonShippingOnly,
                    symbolTargetType: SymbolTargetType,
                    isolated: false,
                    filenamesToExclude: FilesToExclude,
                    flatten: Flatten));

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
                            symbolTargetType: SymbolTargetType,
                            latestLinkShortUrlPrefix: LatestLinkShortUrlPrefix,
                            @internal: false,
                            allowOverwrite: true,
                            filenamesToExclude: FilesToExclude,
                            flatten: Flatten));
                }

                targetFeedConfigs.Add(
                    new TargetFeedConfig(
                        TargetFeedContentType.Checksum,
                        ChecksumsTargetStaticFeed,
                        FeedType.AzureStorageFeed,
                        ChecksumsAzureAccountKey,
                        isolated: true,
                        symbolTargetType: SymbolTargetType,
                        latestLinkShortUrlPrefix: LatestLinkShortUrlPrefix,
                        @internal: false,
                        allowOverwrite: true,
                        filenamesToExclude: FilesToExclude,
                        flatten: Flatten));
            }

            return targetFeedConfigs;
        }
    }
}
