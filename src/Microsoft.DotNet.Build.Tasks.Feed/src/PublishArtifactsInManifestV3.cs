// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
using Microsoft.DotNet.Maestro.Client;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class PublishArtifactsInManifestV3 : PublishArtifactsInManifestBase
    {
        /// <summary>
        /// Comma separated list of Maestro++ Channel IDs to which the build should
        /// be assigned to once the assets are published.
        /// </summary>
        [Required]
        public string TargetChannels { get; set; }

        [Required]
        public string AzureDevOpsFeedsKey { get; set; }

        [Required]
        public string ArtifactsCategory { get; set; }

        [Required]
        public string AzureStorageTargetFeedKey { get; set; }

        [Required]
        public string InstallersFeedKey { get; set; }

        [Required]
        public string CheckSumsFeedKey { get; set;  }

        public bool PublishInstallersAndChecksums { get; set; }

        public override bool Execute()
        {
            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }

        public override async Task<bool> ExecuteAsync()
        {
            if (!AreAllRequiredPropertiesSet())
            {
                Log.LogError("Missing required properties. Aborting execution.");
                return false;
            }

            try 
            {
                var targetChannelsIds = TargetChannels.Split(',').Select(ci => int.Parse(ci));

                foreach (var targetChannelId in targetChannelsIds)
                {
                    TargetChannelConfig targetChannelConfig = PublishingConstants.ChannelInfos
                        .Where(ci => 
                            ci.Id == targetChannelId && 
                            (ci.PublishingInfraVersion == PublishingInfraVersion.All || ci.PublishingInfraVersion == PublishingInfraVersion.Next))
                        .FirstOrDefault();

                    // Invalid channel ID was supplied
                    if (targetChannelConfig.Equals(default(TargetChannelConfig)))
                    {
                        Log.LogError($"Channel with ID '{targetChannelId}' is not configured to be published to.");
                        return false;
                    }

                    var targetFeedsSetup = new SetupTargetFeedConfigV3(
                        InternalBuild,
                        BuildModel.Identity.IsStable.Equals("true", System.StringComparison.OrdinalIgnoreCase),
                        BuildModel.Identity.Name,
                        BuildModel.Identity.Commit,
                        ArtifactsCategory,
                        AzureStorageTargetFeedKey,
                        PublishInstallersAndChecksums,
                        targetChannelConfig.InstallersFeed,
                        InstallersFeedKey,
                        targetChannelConfig.ChecksumsFeed,
                        CheckSumsFeedKey,
                        targetChannelConfig.ShippingFeed,
                        targetChannelConfig.TransportFeed,
                        targetChannelConfig.SymbolsFeed,
                        targetChannelConfig.AkaMSChannelName,
                        AzureDevOpsFeedsKey,
                        BuildEngine = this.BuildEngine);

                    // Fetch Maestro record of the build. We're going to use it to get the BAR ID
                    // of the assets being published so we can add a new location for them.
                    IMaestroApi client = ApiFactory.GetAuthenticated(MaestroApiEndpoint, BuildAssetRegistryToken);
                    Maestro.Client.Models.Build buildInformation = await client.Builds.GetBuildAsync(BARBuildId);
                    Dictionary<string, List<Asset>> buildAssets = CreateBuildAssetDictionary(buildInformation);

                    var targetFeedConfigs = targetFeedsSetup.Setup();

                    // No target feeds to publish to, very likely this is an error
                    if (targetFeedConfigs.Count() == 0)
                    {
                        Log.LogError($"No target feeds were found to publish the assets to.");
                        return false;
                    }

                    foreach (var feedConfig in targetFeedConfigs)
                    {
                        TargetFeedContentType categoryKey = feedConfig.ContentType;
                        if (!FeedConfigs.TryGetValue(categoryKey, out _))
                        {
                            FeedConfigs[categoryKey] = new List<TargetFeedConfig>();
                        }
                        FeedConfigs[categoryKey].Add(feedConfig);
                    }

                    SplitArtifactsInCategories(BuildModel);

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
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
