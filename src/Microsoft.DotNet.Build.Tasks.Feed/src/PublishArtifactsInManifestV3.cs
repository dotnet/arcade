// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
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

            var targetChannelsIds = TargetChannels.Split(",").Select(ci => int.Parse(ci));

            foreach (var targetChannelid in targetChannelsIds)
            {
                TargetChannelConfig targetChannelConfig = ChannelInfos
                    .Where(ci => ci.Id == targetChannelid)
                    .FirstOrDefault();

                List<BuildModel> buildModels = new List<BuildModel>();
                foreach (var assetManifestPath in AssetManifestPaths)
                {
                    Log.LogMessage(MessageImportance.High, $"Publishing artifacts in {assetManifestPath.ItemSpec}.");
                    string fileName = assetManifestPath.ItemSpec;
                    
                    if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
                    {
                        Log.LogError($"Problem reading asset manifest path from '{fileName}'");
                    }
                    else
                    {
                        buildModels.Add(BuildManifestUtil.ManifestFileToModel(assetManifestPath.ItemSpec, Log));
                    }
                }

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

                    var targetFeedConfigs = targetFeedsSetup.Setup();
                }

            }

            return await Task.FromResult(true);
        }
    }
}
