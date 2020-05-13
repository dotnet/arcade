// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
using Microsoft.DotNet.Maestro.Client;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System.Collections.Generic;
using System.IO;
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
        public string TargetChannels { get; set; }

        public override bool Execute()
        {
            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }

        public override async Task<bool> ExecuteAsync()
        {
            if (string.IsNullOrEmpty(TargetChannels))
            {
                Log.LogError("The list of Maestro++ target channels ID that the build should be promoted to is required.");
                return false;
            }

            var targetChannelsIds = TargetChannels.Split(',').Select(ci => int.Parse(ci));

            foreach (var targetChannelid in targetChannelsIds)
            {
                TargetChannelConfig targetChannelConfig = ChannelInfos
                    .Where(ci => ci.Id == targetChannelid)
                    .FirstOrDefault();

                List<BuildModel> buildModels = CreateBuildModels();

                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                foreach (var build in buildModels)
                {
                    var targetFeedsSetup = new SetupTargetFeedConfigV3(
                        false,
                        build.Identity.IsStable.Equals("true", System.StringComparison.OrdinalIgnoreCase),
                        build.Identity.Name,
                        build.Identity.Commit,
                        "artifact category",
                        "azure storage pat",
                        false, //"publish installers and checksums",
                        "installers target static feed",
                        "installers azure account key",
                        "checksums target static feed",
                        "checksums azure account key",
                        targetChannelConfig.ShippingFeed,
                        "azdo shipping feed key",
                        targetChannelConfig.TransportFeed,
                        "azdo transport feed key",
                        targetChannelConfig.SymbolsFeed,
                        "azdo symbols feed key",
                        targetChannelConfig.AkaMSChannelName,
                        "azdo target feed pat",
                        BuildEngine = this.BuildEngine);

                    // Fetch Maestro record of the build. We're going to use it to get the BAR ID
                    // of the assets being published so we can add a new location for them.
                    IMaestroApi client = ApiFactory.GetAuthenticated(MaestroApiEndpoint, BuildAssetRegistryToken);
                    Maestro.Client.Models.Build buildInformation = await client.Builds.GetBuildAsync(BARBuildId);
                    Dictionary<string, List<Asset>> buildAssets = CreateBuildAssetDictionary(buildInformation);

                    var targetFeedConfigs = targetFeedsSetup.Setup();

                    foreach (var feedConfig in targetFeedConfigs)
                    {
                        TargetFeedContentType categoryKey = feedConfig.ContentType;
                        if (!FeedConfigs.TryGetValue(categoryKey, out _))
                        {
                            FeedConfigs[categoryKey] = new List<TargetFeedConfig>();
                        }
                        FeedConfigs[categoryKey].Add(feedConfig);
                    }

                    // Return errors from parsing FeedConfig
                    if (Log.HasLoggedErrors)
                    {
                        return false;
                    }

                    foreach (var buildModel in buildModels)
                    {
                        SplitArtifactsInCategories(buildModel);
                    }

                    // Return errors from the safety checks
                    if (Log.HasLoggedErrors)
                    {
                        return false;
                    }

                    await Task.WhenAll(new Task[] {
                        HandlePackagePublishingAsync(buildAssets),
                        HandleBlobPublishingAsync(buildAssets)
                    }
                    );

                    await PersistPendingAssetLocationAsync(client);
                }
            }

            return await Task.FromResult(true);
        }
    }
}
