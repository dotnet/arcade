// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
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

        public TaskLoggingHelper Log { get; }

        public string AzureDevOpsOrg => "dnceng";

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
            List<string> latestLinkShortUrlPrefixes,
            IBuildEngine buildEngine,
            SymbolTargetType symbolTargetType,
            string stablePackagesFeed = null,
            string stableSymbolsFeed = null,
            ImmutableList<string> filesToExclude = null,
            bool flatten = true,
            TaskLoggingHelper log = null) 
            : base(isInternalBuild, isStableBuild, repositoryName, commitSha, null, publishInstallersAndChecksums, null, null, null, null, null, null, null, latestLinkShortUrlPrefixes, null)
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
            Log = log;
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
            return Feeds().Distinct().ToList();
        }

        private IEnumerable<TargetFeedConfig> Feeds()
        {
            // If the build is stable, we need to create two new feeds (if not provided)
            // that can contain stable packages. These packages cannot be pushed to the normal
            // feeds specified in the feed config because it would mean pushing the same package more than once
            // to the same feed on successive builds, which is not allowed.
            if (IsStableBuild)
            {
                CreateStablePackagesFeedIfNeeded();
                CreateStableSymbolsFeedIfNeeded();

                yield return new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    StablePackagesFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    LatestLinkShortUrlPrefixes,
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
                    LatestLinkShortUrlPrefixes,
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

                    // If dealing with a stable build, the package feed targeted for shipping packages and symbols
                    // should be skipped, as it is added above.
                    if (IsStableBuild && ((type is TargetFeedContentType.Package && spec.Assets == AssetSelection.ShippingOnly) || type is TargetFeedContentType.Symbols))
                    {
                        continue;
                    }

                    var oldFeed = spec.FeedUrl;
                    var feed = GetFeedOverride(oldFeed);
                    if (type is TargetFeedContentType.Package &&
                        spec.Assets == AssetSelection.NonShippingOnly &&
                        FeedOverrides.TryGetValue("transport-packages", out string newFeed))
                    {
                        feed = newFeed;
                    }
                    else if (type is TargetFeedContentType.Package &&
                        spec.Assets == AssetSelection.ShippingOnly &&
                        FeedOverrides.TryGetValue("shipping-packages", out newFeed))
                    {
                        feed = newFeed;
                    }
                    var key = GetFeedKey(feed);
                    var sasUri = GetFeedSasUri(feed);
                    if (feed != oldFeed && string.IsNullOrEmpty(key) && string.IsNullOrEmpty(sasUri))
                    {
                        Log?.LogWarning($"No keys found for {feed}, unable to publish to it.");
                        continue;
                    }
                    var feedType = feed.StartsWith("https://pkgs.dev.azure.com")
                        ? FeedType.AzDoNugetFeed
                        : (sasUri != null ? FeedType.AzureStorageContainer : FeedType.AzureStorageFeed);
                    yield return new TargetFeedConfig(
                        type,
                        sasUri ?? feed,
                        feedType,
                        sasUri == null ? key : null,
                        LatestLinkShortUrlPrefixes,
                        spec.Assets,
                        false,
                        IsInternalBuild,
                        false,
                        SymbolTargetType,
                        filenamesToExclude: FilesToExclude,
                        flatten: Flatten
                    );
                }
            }
        }

        /// <summary>
        /// Create the stable symbol packages feed if one is not already explicitly provided
        /// </summary>
        /// <exception cref="Exception">Throws if the feed cannot be created</exception>
        private void CreateStableSymbolsFeedIfNeeded()
        {
            if (string.IsNullOrEmpty(StableSymbolsFeed))
            {
                var symbolsFeedTask = new CreateAzureDevOpsFeed()
                {
                    BuildEngine = BuildEngine,
                    AzureDevOpsOrg = AzureDevOpsOrg,
                    AzureDevOpsProject = IsInternalBuild ? "internal" : "public",
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
        }

        /// <summary>
        /// Create the stable packages feed if one is not already explicitly provided
        /// </summary>
        /// <exception cref="Exception">Throws if the feed cannot be created</exception>
        private void CreateStablePackagesFeedIfNeeded()
        {
            if (string.IsNullOrEmpty(StablePackagesFeed))
            {
                var packagesFeedTask = new CreateAzureDevOpsFeed()
                {
                    BuildEngine = BuildEngine,
                    AzureDevOpsOrg = AzureDevOpsOrg,
                    AzureDevOpsProject = IsInternalBuild ? "internal" : "public",
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
        }

        private string GetFeedOverride(string feed)
        {
            foreach (var prefix in FeedOverrides.Keys.OrderByDescending(f => f.Length))
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
            foreach (var prefix in FeedSasUris.Keys.OrderByDescending(f => f.Length))
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
            foreach (var prefix in FeedKeys.Keys.OrderByDescending(f => f.Length))
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
