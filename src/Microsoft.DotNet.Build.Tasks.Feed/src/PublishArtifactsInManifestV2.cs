// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Maestro.Client;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    /// <summary>
    /// The intended use of this task is to push artifacts described in
    /// a build manifest to a static package feed.
    /// </summary>
    public class PublishArtifactsInManifestV2 : PublishArtifactsInManifestBase
    {
        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public override async Task<bool> ExecuteAsync()
        {
            try
            {
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
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
