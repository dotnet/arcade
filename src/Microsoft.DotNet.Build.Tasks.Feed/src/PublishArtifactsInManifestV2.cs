// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Feed.model;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
using Microsoft.DotNet.Maestro.Client;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    /// <summary>
    /// The intended use of this task is to push artifacts described in
    /// a build manifest to a static package feed.
    /// </summary>
    public class PublishArtifactsInManifestV2 : PublishArtifactsInManifestBase
    {
        public ITaskItem[] TargetFeedConfig { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public override async Task<bool> ExecuteAsync()
        {
            try
            {
                // Fetch Maestro record of the build. We're going to use it to get the BAR ID
                // of the assets being published so we can add a new location for them.
                IMaestroApi client = ApiFactory.GetAuthenticated(MaestroApiEndpoint, BuildAssetRegistryToken);
                Maestro.Client.Models.Build buildInformation = await client.Builds.GetBuildAsync(BARBuildId);
                Dictionary<string, List<Asset>> buildAssets = CreateBuildAssetDictionary(buildInformation);

                await ParseTargetFeedConfigAsync();

                // Return errors from parsing FeedConfig
                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                SplitArtifactsInCategories(BuildModel);

                // Return errors from the safety checks
                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                await Task.WhenAll(new Task[] {
                    HandlePackagePublishingAsync(buildAssets),
                    HandleBlobPublishingAsync(buildAssets)
                });

                await PersistPendingAssetLocationAsync(client);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        ///     Parse out the input TargetFeedConfig into a dictionary of FeedConfig types
        /// </summary>
        public async Task ParseTargetFeedConfigAsync()
        {
            using (HttpClient httpClient = new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true }))
            {
                foreach (var fc in TargetFeedConfig)
                {
                    string targetFeedUrl = fc.GetMetadata(nameof(Model.TargetFeedConfig.TargetURL));
                    string feedKey = fc.GetMetadata(nameof(Model.TargetFeedConfig.Token));
                    string type = fc.GetMetadata(nameof(Model.TargetFeedConfig.Type));

                    if (string.IsNullOrEmpty(targetFeedUrl) ||
                        string.IsNullOrEmpty(feedKey) ||
                        string.IsNullOrEmpty(type))
                    {
                        Log.LogError($"Invalid FeedConfig entry. {nameof(Model.TargetFeedConfig.TargetURL)}='{targetFeedUrl}' {nameof(Model.TargetFeedConfig.Type)}='{type}' {nameof(Model.TargetFeedConfig.Token)}='{feedKey}'");
                        continue;
                    }

                    if (!targetFeedUrl.EndsWith(PublishingConstants.ExpectedFeedUrlSuffix))
                    {
                        Log.LogError($"Exepcted that feed '{targetFeedUrl}' would end in {PublishingConstants.ExpectedFeedUrlSuffix}");
                        continue;
                    }

                    if (!Enum.TryParse<FeedType>(type, true, out FeedType feedType))
                    {
                        Log.LogError($"Invalid feed config type '{type}'. Possible values are: {string.Join(", ", Enum.GetNames(typeof(FeedType)))}");
                        continue;
                    }

                    var feedConfig = new TargetFeedConfig()
                    {
                        TargetURL = targetFeedUrl,
                        Type = feedType,
                        Token = feedKey
                    };

                    string assetSelection = fc.GetMetadata(nameof(Model.TargetFeedConfig.AssetSelection));
                    if (!string.IsNullOrEmpty(assetSelection))
                    {
                        if (!Enum.TryParse<AssetSelection>(assetSelection, true, out AssetSelection selection))
                        {
                            Log.LogError($"Invalid feed config asset selection '{type}'. Possible values are: {string.Join(", ", Enum.GetNames(typeof(AssetSelection)))}");
                            continue;
                        }
                        feedConfig.AssetSelection = selection;
                    }

                    // To determine whether a feed is internal, we allow the user to
                    // specify the value explicitly.
                    string feedIsInternal = fc.GetMetadata(nameof(Model.TargetFeedConfig.Internal));
                    if (!string.IsNullOrEmpty(feedIsInternal))
                    {
                        if (!bool.TryParse(feedIsInternal, out bool feedSetting))
                        {
                            Log.LogError($"Invalid feed config '{nameof(Model.TargetFeedConfig.Internal)}' setting.  Must be 'true' or 'false'.");
                            continue;
                        }
                        feedConfig.Internal = feedSetting;
                    }
                    else
                    {
                        bool? isPublicFeed = await GeneralUtils.IsFeedPublicAsync(feedConfig.TargetURL, httpClient, Log);
                        if (!isPublicFeed.HasValue)
                        {
                            continue;
                        }
                        else
                        {
                            feedConfig.Internal = !isPublicFeed.Value;
                        }
                    }

                    CheckForInternalBuildsOnPublicFeeds(feedConfig);

                    string feedIsIsolated = fc.GetMetadata(nameof(Model.TargetFeedConfig.Isolated));
                    if (!string.IsNullOrEmpty(feedIsIsolated))
                    {
                        if (!bool.TryParse(feedIsIsolated, out bool feedSetting))
                        {
                            Log.LogError($"Invalid feed config '{nameof(Model.TargetFeedConfig.Isolated)}' setting.  Must be 'true' or 'false'.");
                            continue;
                        }
                        feedConfig.Isolated = feedSetting;
                    }

                    string allowOverwriteOnFeed = fc.GetMetadata(nameof(Model.TargetFeedConfig.AllowOverwrite));
                    if (!string.IsNullOrEmpty(allowOverwriteOnFeed))
                    {
                        if (!bool.TryParse(allowOverwriteOnFeed, out bool feedSetting))
                        {
                            Log.LogError($"Invalid feed config '{nameof(Model.TargetFeedConfig.AllowOverwrite)}' setting.  Must be 'true' or 'false'.");
                            continue;
                        }
                        feedConfig.AllowOverwrite = feedSetting;
                    }

                    string latestLinkShortUrlPrefix = fc.GetMetadata(nameof(Model.TargetFeedConfig.LatestLinkShortUrlPrefix));
                    if (!string.IsNullOrEmpty(latestLinkShortUrlPrefix))
                    {
                        // Verify other inputs are provided
                        if (string.IsNullOrEmpty(AkaMSClientId) ||
                            string.IsNullOrEmpty(AkaMSClientSecret) ||
                            string.IsNullOrEmpty(AkaMSTenant) ||
                            string.IsNullOrEmpty(AkaMsOwners) ||
                            string.IsNullOrEmpty(AkaMSCreatedBy))
                        {
                            Log.LogError($"If a short url path is provided, please provide {nameof(AkaMSClientId)}, {nameof(AkaMSClientSecret)}, " +
                                $"{nameof(AkaMSTenant)}, {nameof(AkaMsOwners)}, {nameof(AkaMSCreatedBy)}");
                            continue;
                        }
                        feedConfig.LatestLinkShortUrlPrefix = latestLinkShortUrlPrefix;

                        // Set up the link manager if it hasn't already been done
                        if (LinkManager == null)
                        {
                            LinkManager = new LatestLinksManager(AkaMSClientId, AkaMSClientSecret, AkaMSTenant, AkaMSGroupOwner, AkaMSCreatedBy, AkaMsOwners, Log);
                        }
                    }

                    if (!Enum.TryParse(fc.ItemSpec, ignoreCase: true, out TargetFeedContentType categoryKey))
                    {
                        Log.LogError($"Invalid target feed config category '{fc.ItemSpec}'.");
                    }

                    if (!FeedConfigs.TryGetValue(categoryKey, out _))
                    {
                        FeedConfigs[categoryKey] = new List<TargetFeedConfig>();
                    }

                    FeedConfigs[categoryKey].Add(feedConfig);
                }
            }
        }
    }
}
