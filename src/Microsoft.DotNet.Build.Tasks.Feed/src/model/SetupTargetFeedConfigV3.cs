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
                    new TargetFeedConfig()
                    {
                        ContentType = TargetFeedContentType.Checksum,
                        TargetURL = ChecksumsTargetStaticFeed,
                        Isolated = false,
                        Type = FeedType.AzureStorageFeed,
                        Token = ChecksumsAzureAccountKey,
                        LatestLinkShortUrlPrefix = LatestLinkShortUrlPrefix
                    });

                foreach (var ct in Installers)
                {
                    targetFeedConfigs.Add(
                        new TargetFeedConfig()
                        {
                            ContentType = ct,
                            TargetURL = InstallersTargetStaticFeed,
                            Isolated = false,
                            Type = FeedType.AzureStorageFeed,
                            Token = InstallersAzureAccountKey,
                            LatestLinkShortUrlPrefix = LatestLinkShortUrlPrefix
                        });
                }
            }

            targetFeedConfigs.Add(
                new TargetFeedConfig()
                {
                    ContentType = TargetFeedContentType.Package,
                    TargetURL = AzureDevOpsStaticShippingFeed,
                    Isolated = false,
                    Type = FeedType.AzDoNugetFeed,
                    Token = AzureDevOpsFeedsKey,
                    AssetSelection = AssetSelection.ShippingOnly
                });

            targetFeedConfigs.Add(
                new TargetFeedConfig()
                {
                    ContentType = TargetFeedContentType.Package,
                    TargetURL = AzureDevOpsStaticTransportFeed,
                    Isolated = false,
                    Type = FeedType.AzDoNugetFeed,
                    Token = AzureDevOpsFeedsKey,
                    AssetSelection = AssetSelection.NonShippingOnly
                });

            return targetFeedConfigs;
        }

        private List<TargetFeedConfig>  NonStableInternalFeeds()
        {
            List<TargetFeedConfig> targetFeedConfigs = new List<TargetFeedConfig>
            {
                new TargetFeedConfig()
                {
                    AssetSelection = AssetSelection.ShippingOnly,
                    ContentType = TargetFeedContentType.Package,
                    TargetURL = AzureDevOpsStaticShippingFeed,
                    Isolated = false,
                    Type = FeedType.AzDoNugetFeed,
                    Token = AzureDevOpsFeedsKey
                },

                new TargetFeedConfig()
                {
                    AssetSelection = AssetSelection.NonShippingOnly,
                    ContentType = TargetFeedContentType.Package,
                    TargetURL = AzureDevOpsStaticTransportFeed,
                    Isolated = false,
                    Type = FeedType.AzDoNugetFeed,
                    Token = AzureDevOpsFeedsKey
                },

                new TargetFeedConfig()
                {
                    ContentType = TargetFeedContentType.Symbols,
                    TargetURL = AzureDevOpsStaticSymbolsFeed,
                    Isolated = false,
                    Type = FeedType.AzDoNugetFeed,
                    Token = AzureDevOpsFeedsKey
                }
            };

            if (PublishInstallersAndChecksums)
            {
                foreach (var ct in Installers)
                {
                    targetFeedConfigs.Add(
                        new TargetFeedConfig()
                        {
                            ContentType = ct,
                            TargetURL = InstallersTargetStaticFeed,
                            Isolated = false,
                            Type = FeedType.AzureStorageFeed,
                            Token = InstallersAzureAccountKey
                        });
                }

                targetFeedConfigs.Add(
                    new TargetFeedConfig()
                    {
                        ContentType = TargetFeedContentType.Checksum,
                        TargetURL = ChecksumsTargetStaticFeed,
                        Isolated = false,
                        Type = FeedType.AzureStorageFeed,
                        Token = ChecksumsAzureAccountKey
                    });
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
                new TargetFeedConfig()
                {
                    ContentType = TargetFeedContentType.Package,
                    AssetSelection = AssetSelection.ShippingOnly,
                    TargetURL = StablePackagesFeed,
                    Isolated = true,
                    Type = FeedType.AzDoNugetFeed,
                    Token = AzureDevOpsFeedsKey
                });

            targetFeedConfigs.Add(
                new TargetFeedConfig()
                {
                    AssetSelection = AssetSelection.All,
                    ContentType = TargetFeedContentType.Symbols,
                    TargetURL = StableSymbolsFeed,
                    Isolated = true,
                    Type = FeedType.AzDoNugetFeed,
                    Token = AzureDevOpsFeedsKey
                });

            targetFeedConfigs.Add(
                new TargetFeedConfig()
                {
                    AssetSelection = AssetSelection.NonShippingOnly,
                    ContentType = TargetFeedContentType.Package,
                    TargetURL = AzureDevOpsStaticTransportFeed,
                    Isolated = false,
                    Type = FeedType.AzDoNugetFeed,
                    Token = AzureDevOpsFeedsKey
                });

            if (PublishInstallersAndChecksums)
            {
                foreach (var ct in Installers)
                {
                    targetFeedConfigs.Add(
                        new TargetFeedConfig()
                        {
                            ContentType = ct,
                            TargetURL = InstallersTargetStaticFeed,
                            Isolated = true,
                            AllowOverwrite = true,
                            Type = FeedType.AzureStorageFeed,
                            Token = InstallersAzureAccountKey
                        });
                }

                targetFeedConfigs.Add(
                    new TargetFeedConfig()
                    {
                        ContentType = TargetFeedContentType.Checksum,
                        TargetURL = ChecksumsTargetStaticFeed,
                        Isolated = true,
                        AllowOverwrite = true,
                        Type = FeedType.AzureStorageFeed,
                        Token = ChecksumsAzureAccountKey
                    });
            }

            return targetFeedConfigs;
        }
    }
}
