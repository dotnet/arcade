// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Feed.Model;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class SetupTargetFeedConfigV3 : SetupTargetFeedConfigBase
    {
        private readonly TargetChannelConfig _targetChannelConfig;

        private IBuildEngine BuildEngine { get; }
        
        private string StablePackagesFeed { get; set; }
        
        private string StableSymbolsFeed { get; set; }

        private SymbolTargetType SymbolTargetType { get; }

        private ImmutableList<string> FilesToExclude { get; }

        private bool Flatten { get; }

        public SetupTargetFeedConfigV3(
            TargetChannelConfig targetChannelConfig,
            bool isInternalBuild,
            bool isStableBuild,
            string repositoryName,
            string commitSha,
            bool publishInstallersAndChecksums,
            ITaskItem[] feedKeys,
            ITaskItem[] feedSasUris,
            ITaskItem[] feedOverrides,
            string latestLinkShortUrlPrefix,
            IBuildEngine buildEngine,
            SymbolTargetType symbolTargetType,
            string stablePackagesFeed = null,
            string stableSymbolsFeed = null,
            ImmutableList<string> filesToExclude = null,
            bool flatten = true) 
            : base(isInternalBuild, isStableBuild, repositoryName, commitSha, null, publishInstallersAndChecksums, null, null, null, null, null, null, null, latestLinkShortUrlPrefix, null)
        {
            _targetChannelConfig = targetChannelConfig;
            BuildEngine = buildEngine;
            StableSymbolsFeed = stableSymbolsFeed;
            StablePackagesFeed = stablePackagesFeed;
            SymbolTargetType = symbolTargetType;
            FilesToExclude = filesToExclude ?? ImmutableList<string>.Empty;
            Flatten = flatten;
            FeedKeys = feedKeys.ToImmutableDictionary(i => i.ItemSpec, i => i.GetMetadata("Key"));
            FeedSasUris = feedSasUris.ToImmutableDictionary(i => i.ItemSpec, i => ConvertFromBase64(i.GetMetadata("Base64Uri")));
            FeedOverrides = feedOverrides.ToImmutableDictionary(i => i.ItemSpec, i => i.GetMetadata("Replacement"));
            AzureDevOpsFeedsKey = FeedKeys.TryGetValue("https://pkgs.dev.azure.com/dnceng", out string key) ? key : null;
        }

        private static string ConvertFromBase64(string value)
        {
            if (value == null)
            {
                return null;
            }
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }

        public ImmutableDictionary<string, string> FeedOverrides { get; set; }

        public ImmutableDictionary<string, string> FeedSasUris { get; set; }

        public ImmutableDictionary<string, string> FeedKeys { get; set; }

        public override List<TargetFeedConfig> Setup()
        {
            return Feeds().ToList();
        }

        private IEnumerable<TargetFeedConfig> Feeds()
        {
            if (IsStableBuild)
            {
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

                yield return new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    StablePackagesFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    LatestLinkShortUrlPrefix,
                    assetSelection: AssetSelection.ShippingOnly,
                    symbolTargetType: SymbolTargetType,
                    isolated: true,
                    @internal: IsInternalBuild,
                    filenamesToExclude: FilesToExclude,
                    flatten: Flatten);

                yield return new TargetFeedConfig(
                    TargetFeedContentType.Symbols,
                    StableSymbolsFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    LatestLinkShortUrlPrefix,
                    symbolTargetType: SymbolTargetType,
                    isolated: true,
                    @internal: IsInternalBuild,
                    filenamesToExclude: FilesToExclude,
                    flatten: Flatten);
            }
            foreach (var spec in _targetChannelConfig.TargetFeeds)
            {
                foreach (var type in spec.ContentTypes)
                {
                    if (!PublishInstallersAndChecksums)
                    {
                        if (PublishingConstants.InstallersAndChecksums.Contains(type))
                        {
                            continue;
                        }
                    }
                    if (IsStableBuild && ((type is TargetFeedContentType.Package && spec.Assets == AssetSelection.ShippingOnly) || type is TargetFeedContentType.Symbols))
                    {
                        // stable build shipping packages and symbols were handled above
                        continue;
                    }

                    var feed = spec.FeedUrl;
                    feed = GetFeedOverride(feed);
                    if (type is TargetFeedContentType.Package &&
                        spec.Assets == AssetSelection.ShippingOnly &&
                        FeedOverrides.TryGetValue("transport-packages", out string newFeed))
                    {
                        feed = newFeed;
                    }
                    else if (type is TargetFeedContentType.Package &&
                        spec.Assets == AssetSelection.NonShippingOnly &&
                        FeedOverrides.TryGetValue("shipping-packages", out newFeed))
                    {
                        feed = newFeed;
                    }
                    var key = GetFeedKey(feed);
                    var sasUri = GetFeedSasUri(feed);
                    var feedType = feed.StartsWith("https://pkgs.dev.azure.com")
                        ? FeedType.AzDoNugetFeed
                        : (sasUri != null ? FeedType.AzureStorageContainer : FeedType.AzureStorageFeed);
                    yield return new TargetFeedConfig(
                        type,
                        sasUri ?? feed,
                        feedType,
                        key,
                        LatestLinkShortUrlPrefix,
                        spec.Assets,
                        false,
                        IsInternalBuild,
                        false,
                        filenamesToExclude: FilesToExclude,
                        flatten: Flatten
                    );
                }
            }
        }

        private string GetFeedOverride(string feed)
        {
            foreach (var prefix in FeedOverrides.Keys)
            {
                if (feed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return FeedOverrides[prefix];
                }
            }

            return feed;
        }

        private string GetFeedSasUri(string feed)
        {
            foreach (var prefix in FeedSasUris.Keys)
            {
                if (feed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return FeedSasUris[prefix];
                }
            }

            return null;
        }

        private string GetFeedKey(string feed)
        {
            foreach (var prefix in FeedKeys.Keys)
            {
                if (feed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return FeedKeys[prefix];
                }
            }

            return null;
        }
    }
}
