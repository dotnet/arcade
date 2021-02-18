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

        private SymbolTargetType SymbolTargetType { get; set; }

        private List<string> FilesToExclude { get; }

        private bool Flatten { get; }

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
            SymbolTargetType symbolTargetType,
        string stablePackagesFeed = null,
            string stableSymbolsFeed = null,
            List<string> filesToExclude = null,
            bool flatten = true) 
            : base(isInternalBuild, isStableBuild, repositoryName, commitSha, azureStorageTargetFeedPAT, publishInstallersAndChecksums, installersTargetStaticFeed, installersAzureAccountKey, checksumsTargetStaticFeed, checksumsAzureAccountKey, azureDevOpsStaticShippingFeed, azureDevOpsStaticTransportFeed, azureDevOpsStaticSymbolsFeed, latestLinkShortUrlPrefix, azureDevOpsFeedsKey)
        {
            BuildEngine = buildEngine;
            StableSymbolsFeed = stableSymbolsFeed;
            StablePackagesFeed = stablePackagesFeed;
            SymbolTargetType = symbolTargetType;
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
                        LatestLinkShortUrlPrefix,
                        symbolTargetType: SymbolTargetType,
                        filenamesToExclude: FilesToExclude,
                        flatten: Flatten));

                foreach (var contentType in Installers)
                {
                    targetFeedConfigs.Add(
                        new TargetFeedConfig(
                            contentType,
                            InstallersTargetStaticFeed,
                            FeedType.AzureStorageFeed,
                            InstallersAzureAccountKey,
                            LatestLinkShortUrlPrefix,
                            symbolTargetType: SymbolTargetType,
                            filenamesToExclude: FilesToExclude,
                            flatten: Flatten));
                }
            }

            targetFeedConfigs.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Symbols,
                    PublishingConstants.LegacyDotNetBlobFeedURL,
                    FeedType.AzureStorageFeed,
                    AzureStorageTargetFeedPAT,
                    symbolTargetType: SymbolTargetType,
                    filenamesToExclude: FilesToExclude,
                    flatten: Flatten));

            targetFeedConfigs.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    AzureDevOpsStaticShippingFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    assetSelection: AssetSelection.ShippingOnly,
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
                    symbolTargetType: SymbolTargetType,
                    filenamesToExclude: FilesToExclude,
                    flatten: Flatten));

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
                    symbolTargetType: SymbolTargetType,
                    @internal: true,
                    filenamesToExclude: FilesToExclude,
                    flatten: Flatten
                ),

                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    AzureDevOpsStaticTransportFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    assetSelection: AssetSelection.NonShippingOnly,
                    symbolTargetType: SymbolTargetType,
                    @internal: true,
                    filenamesToExclude: FilesToExclude,
                    flatten: Flatten
                ),

                new TargetFeedConfig(
                    TargetFeedContentType.Symbols,
                    AzureDevOpsStaticSymbolsFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    symbolTargetType: SymbolTargetType,
                    @internal: true,
                    filenamesToExclude: FilesToExclude,
                    flatten: Flatten)
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
                            symbolTargetType: SymbolTargetType,
                            @internal: true,
                            filenamesToExclude: FilesToExclude,
                            flatten: Flatten
                        ));
                }

                targetFeedConfigs.Add(
                    new TargetFeedConfig(
                        TargetFeedContentType.Checksum,
                        ChecksumsTargetStaticFeed,
                        FeedType.AzureStorageFeed,
                        ChecksumsAzureAccountKey,
                        symbolTargetType: SymbolTargetType,
                        @internal: true,
                        filenamesToExclude: FilesToExclude,
                        flatten: Flatten
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
                        @internal: false,
                        allowOverwrite: true,
                        filenamesToExclude: FilesToExclude,
                        flatten: Flatten));
            }

            return targetFeedConfigs;
        }
    }
}
