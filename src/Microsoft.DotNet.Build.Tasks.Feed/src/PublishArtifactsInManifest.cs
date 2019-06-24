// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Maestro.Client;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    /// <summary>
    /// The intended use of this task is to push artifacts described in
    /// a build manifest to a static package feed.
    /// </summary>
    public class PublishArtifactsInManifest : MSBuild.Task
    {
        /// <summary>
        /// Configuration telling which target feed to use for each artifact category.
        /// ItemSpec: ArtifactCategory
        /// Metadata TargetURL: target URL where assets of this category should be published to.
        /// Metadata Type: type of the target feed.
        /// Metadata Token: token to be used for publishing to target feed.
        /// </summary>
        [Required]
        public ITaskItem[] TargetFeedConfig { get; set; }

        /// <summary>
        /// Full path to the assets to publish manifest.
        /// </summary>
        [Required]
        public string AssetManifestPath { get; set; }

        /// <summary>
        /// Full path to the folder containing blob assets.
        /// </summary>
        [Required]
        public string BlobAssetsBasePath { get; set; }

        /// <summary>
        /// Full path to the folder containing package assets.
        /// </summary>
        [Required]
        public string PackageAssetsBasePath { get; set; }

        /// <summary>
        /// ID of the build (in BAR/Maestro) that produced the artifacts being published.
        /// This might change in the future as we'll probably fetch this ID from the manifest itself.
        /// </summary>
        [Required]
        public int BARBuildId { get; set; }

        /// <summary>
        /// Access point to the Maestro API to be used for accessing BAR.
        /// </summary>
        [Required]
        public string MaestroApiEndpoint { get; set; }

        /// <summary>
        /// Authentication token to be used when interacting with Maestro API.
        /// </summary>
        [Required]
        public string BuildAssetRegistryToken { get; set; }

        /// <summary>
        /// Directory where "nuget.exe" is installed. This will be used to publish packages.
        /// </summary>
        [Required]
        public string NugetPath { get; set; }

        private readonly Dictionary<string, FeedConfig> FeedConfigs = new Dictionary<string, FeedConfig>();

        private readonly Dictionary<string, List<PackageArtifactModel>> PackagesByCategory = new Dictionary<string, List<PackageArtifactModel>>();

        private readonly Dictionary<string, List<BlobArtifactModel>> BlobsByCategory = new Dictionary<string, List<BlobArtifactModel>>();


        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Publishing artifacts to feed.");

                if (string.IsNullOrWhiteSpace(AssetManifestPath) || !File.Exists(AssetManifestPath))
                {
                    Log.LogError($"Problem reading asset manifest path from '{AssetManifestPath}'");
                }

                if (!Directory.Exists(BlobAssetsBasePath))
                {
                    Log.LogError($"Problem reading blob assets from {BlobAssetsBasePath}");
                }

                if (!Directory.Exists(PackageAssetsBasePath))
                {
                    Log.LogError($"Problem reading package assets from {PackageAssetsBasePath}");
                }

                var buildModel = BuildManifestUtil.ManifestFileToModel(AssetManifestPath, Log);

                // Parsing the manifest may fail for several reasons
                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                // Fetch Maestro record of the build. We're going to use it to get the BAR ID
                // of the assets being published so we can add a new location for them.
                IMaestroApi client = ApiFactory.GetAuthenticated(MaestroApiEndpoint, BuildAssetRegistryToken);
                Maestro.Client.Models.Build buildInformation = await client.Builds.GetBuildAsync(BARBuildId);

                foreach (var fc in TargetFeedConfig)
                {
                    var feedConfig = new FeedConfig()
                    {
                        TargetFeedURL = fc.GetMetadata("TargetURL"),
                        Type = fc.GetMetadata("Type"),
                        FeedKey = fc.GetMetadata("Token")
                    };

                    if (string.IsNullOrEmpty(feedConfig.TargetFeedURL) ||
                        string.IsNullOrEmpty(feedConfig.Type) ||
                        string.IsNullOrEmpty(feedConfig.FeedKey))
                    {
                        Log.LogError($"Invalid FeedConfig entry. TargetURL='{feedConfig.TargetFeedURL}' Type='{feedConfig.Type}' Token='{feedConfig.FeedKey}'");
                    }

                    FeedConfigs.Add(fc.ItemSpec.Trim().ToUpper(), feedConfig);
                }

                // Return errors from parsing FeedConfig
                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                SplitArtifactsInCategories(buildModel);

                await HandlePackagePublishingAsync(client, buildInformation);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }

        private async Task HandlePackagePublishingAsync(IMaestroApi client, Maestro.Client.Models.Build buildInformation)
        {
            foreach (var packagesPerCategory in PackagesByCategory)
            {
                var category = packagesPerCategory.Key;
                var packages = packagesPerCategory.Value;

                if (FeedConfigs.TryGetValue(category, out FeedConfig feedConfig))
                {
                    var feedType = feedConfig.Type.ToUpper();

                    if (feedType.Equals("AZDONUGETFEED"))
                    {
                        await PublishPackagesToAzDoNugetFeedAsync(packages, client, buildInformation, feedConfig);
                    }
                    else if (feedType.Equals("AZURESTORAGEFEED"))
                    {
                        await PublishPackagesToAzureStorageNugetFeedAsync(packages, client, buildInformation, feedConfig);
                    }
                    else
                    {
                        Log.LogError($"Unknown target feed type for category '{category}': '{feedType}'.");
                    }
                }
                else
                {
                    Log.LogError($"No target feed configuration found for artifact category: '{category}'.");
                }
            }
        }

        private void SplitArtifactsInCategories(BuildModel buildModel)
        {
            foreach (var packageAsset in buildModel.Artifacts.Packages)
            {
                string categories = string.Empty;

                if (!packageAsset.Attributes.TryGetValue("Category", out categories))
                {
                    categories = InferCategory(packageAsset.Id);
                }

                foreach (var category in categories.Split(';').Select(c => c.ToUpper()))
                {
                    if (PackagesByCategory.ContainsKey(category))
                    {
                        PackagesByCategory[category].Add(packageAsset);
                    }
                    else
                    {
                        PackagesByCategory[category] = new List<PackageArtifactModel>() { packageAsset };
                    }
                }
            }

            foreach (var blobAsset in buildModel.Artifacts.Blobs)
            {
                string categories = string.Empty;

                if (!blobAsset.Attributes.TryGetValue("Category", out categories))
                {
                    categories = InferCategory(blobAsset.Id);
                }

                foreach (var category in categories.Split(';'))
                {
                    if (BlobsByCategory.ContainsKey(category))
                    {
                        BlobsByCategory[category].Add(blobAsset);
                    }
                    else
                    {
                        BlobsByCategory[category] = new List<BlobArtifactModel>() { blobAsset };
                    }
                }
            }
        }

        private async Task PublishPackagesToAzDoNugetFeedAsync(
            List<PackageArtifactModel> packagesToPublish,
            IMaestroApi client,
            Maestro.Client.Models.Build buildInformation,
            FeedConfig feedConfig)
        {
            foreach (var package in packagesToPublish)
            {
                var assetRecord = buildInformation.Assets
                    .Where(a => a.Name.Equals(package.Id) && a.Version.Equals(package.Version))
                    .FirstOrDefault();

                if (assetRecord == null)
                {
                    Log.LogError($"Asset with Id {package.Id}, Version {package.Version} isn't registered on the BAR Build with ID {BARBuildId}");
                    continue;
                }
                
                var assetWithLocations = await client.Assets.GetAssetAsync(assetRecord.Id);

                if (assetWithLocations?.Locations.Any(al => al.Location.Equals(feedConfig.TargetFeedURL, StringComparison.OrdinalIgnoreCase)) ?? false)
                {
                    Log.LogMessage($"Asset with Id {package.Id}, Version {package.Version} already has location {feedConfig.TargetFeedURL}");
                    continue;
                }

                await client.Assets.AddAssetLocationToAssetAsync(assetRecord.Id, AddAssetLocationToAssetAssetLocationType.NugetFeed, feedConfig.TargetFeedURL);
            }
        }

        private async Task PublishPackagesToAzureStorageNugetFeedAsync(
            List<PackageArtifactModel> packagesToPublish,
            IMaestroApi client,
            Maestro.Client.Models.Build buildInformation,
            FeedConfig feedConfig)
        {
            PackageAssetsBasePath = PackageAssetsBasePath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) 
                + Path.DirectorySeparatorChar;

            var packages = packagesToPublish.Select(p => $"{PackageAssetsBasePath}{p.Id}.{p.Version}.nupkg");

            var blobFeedAction = new BlobFeedAction(feedConfig.TargetFeedURL, feedConfig.FeedKey, Log);
            var pushOptions = new PushOptions
            {
                AllowOverwrite = false,
                PassIfExistingItemIdentical = true
            };

            foreach (var package in packagesToPublish)
            {
                var assetRecord = buildInformation.Assets
                    .Where(a => a.Name.Equals(package.Id) && a.Version.Equals(package.Version))
                    .FirstOrDefault();

                if (assetRecord == null)
                {
                    Log.LogError($"Asset with Id {package.Id}, Version {package.Version} isn't registered on the BAR Build with ID {BARBuildId}");
                    continue;
                }

                var assetWithLocations = await client.Assets.GetAssetAsync(assetRecord.Id);

                if (assetWithLocations?.Locations.Any(al => al.Location.Equals(feedConfig.TargetFeedURL, StringComparison.OrdinalIgnoreCase)) ?? false)
                {
                    Log.LogMessage($"Asset with Id {package.Id}, Version {package.Version} already has location {feedConfig.TargetFeedURL}");
                    continue;
                }

                await client.Assets.AddAssetLocationToAssetAsync(assetRecord.Id, AddAssetLocationToAssetAssetLocationType.NugetFeed, feedConfig.TargetFeedURL);
            }

            await blobFeedAction.PushToFeedAsync(packages, pushOptions);
        }
        
        private string InferCategory(string assetId)
        {
            var extension = Path.GetExtension(assetId).ToUpper();

            var whichCategory = new Dictionary<string, string>()
            {
                { ".NUPKG", "NetCore" },
                { ".PKG", "OSX" },
                { ".DEB", "DEB" },
                { ".RPM", "RPM" },
                { ".NPM", "NODE" },
                { ".ZIP", "BINARYLAYOUT" },
                { ".MSI", "INSTALLER" },
                { ".SHA", "CHECKSUM" },
                { ".POM", "MAVEN" },
                { ".VSIX", "VSIX" },
            };

            if (whichCategory.TryGetValue(extension, out var category))
            {
                return category;
            }
            else
            {
                return "NetCore";
            }
        }
    }

    /// <summary>
    /// Hold properties of a target feed endpoint.
    /// </summary>
    internal class FeedConfig
    {
        public string TargetFeedURL { get; set; }
        public string Type { get; set; }
        public string FeedKey { get; set; }
    }
}
