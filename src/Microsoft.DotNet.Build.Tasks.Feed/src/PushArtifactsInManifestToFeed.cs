// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Maestro.Client;
using System;
using System.Collections.Generic;
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
    public class PushArtifactsInManifestToFeed : MSBuild.Task
    {
        /// <summary>
        /// Target to where the assets should be published.
        /// E.g., https://blab.blob.core.windows.net/container/index.json
        /// </summary>
        [Required]
        public string ExpectedFeedUrl { get; set; }

        /// <summary>
        /// Account key to the feed storage.
        /// </summary>
        [Required]
        public string AccountKey { get; set; }

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
        /// When set to true packages with the same name will be overriden on 
        /// the target feed.
        /// </summary>
        public bool Overwrite { get; set; }

        /// <summary>
        /// Enables idempotency when Overwrite is false.
        /// 
        /// false: (default) Attempting to upload an item that already exists fails.
        /// 
        /// true: When an item already exists, download the existing blob to check if it's
        /// byte-for-byte identical to the one being uploaded. If so, pass. If not, fail.
        /// </summary>
        public bool PassIfExistingItemIdentical { get; set; }

        /// <summary>
        /// Maximum number of concurrent pushes of assets to the flat container.
        /// </summary>
        public int MaxClients { get; set; } = 8;

        /// <summary>
        /// Maximum allowed timeout per upload request to the flat container.
        /// </summary>
        public int UploadTimeoutInMinutes { get; set; } = 5;

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Performing push feeds.");

                if (string.IsNullOrWhiteSpace(ExpectedFeedUrl) || string.IsNullOrWhiteSpace(AccountKey))
                {
                    Log.LogError($"{nameof(ExpectedFeedUrl)} / {nameof(AccountKey)} is not set properly.");
                }
                else if (string.IsNullOrWhiteSpace(AssetManifestPath) || !File.Exists(AssetManifestPath))
                {
                    Log.LogError($"Problem reading asset manifest path from {AssetManifestPath}");
                }
                else if (MaxClients <= 0)
                {
                    Log.LogError($"{nameof(MaxClients)} should be greater than zero.");
                }
                else if (UploadTimeoutInMinutes <= 0)
                {
                    Log.LogError($"{nameof(UploadTimeoutInMinutes)} should be greater than zero.");
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

                var blobFeedAction = new BlobFeedAction(ExpectedFeedUrl, AccountKey, Log);
                var pushOptions = new PushOptions
                {
                    AllowOverwrite = Overwrite,
                    PassIfExistingItemIdentical = PassIfExistingItemIdentical
                };

                if (buildModel.Artifacts.Packages.Any())
                {
                    if (!Directory.Exists(PackageAssetsBasePath))
                    {
                        Log.LogError($"Invalid {nameof(PackageAssetsBasePath)} was supplied: {PackageAssetsBasePath}");
                        return false;
                    }

                    PackageAssetsBasePath = PackageAssetsBasePath.TrimEnd(Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

                    var packages = buildModel.Artifacts.Packages.Select(p => $"{PackageAssetsBasePath}{p.Id}.{p.Version}.nupkg");

                    await blobFeedAction.PushToFeedAsync(packages, pushOptions);

                    foreach (var package in buildModel.Artifacts.Packages)
                    {
                        var assetRecord = buildInformation.Assets
                            .Where(a => a.Name.Equals(package.Id) && a.Version.Equals(package.Version))
                            .Single();

                        if (assetRecord == null)
                        {
                            Log.LogError($"Asset with Id {package.Id}, Version {package.Version} isn't registered on the BAR Build with ID {BARBuildId}");
                            continue;
                        }

                        await client.Assets.AddAssetLocationToAssetAsync(assetRecord.Id.Value, ExpectedFeedUrl, "NugetFeed");
                    }
                }

                if (buildModel.Artifacts.Blobs.Any())
                {
                    if (!Directory.Exists(BlobAssetsBasePath))
                    {
                        Log.LogError($"Invalid {nameof(BlobAssetsBasePath)} was supplied: {BlobAssetsBasePath}");
                        return false;
                    }

                    BlobAssetsBasePath = BlobAssetsBasePath.TrimEnd(Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

                    var blobs = buildModel.Artifacts.Blobs
                        .Select(blob =>
                        {
                            var fileName = Path.GetFileName(blob.Id);
                            return new MSBuild.TaskItem($"{BlobAssetsBasePath}{fileName}", new Dictionary<string, string>
                            {
                                {"RelativeBlobPath", $"{BuildManifestUtil.AssetsVirtualDir}{blob.Id}"}
                            });
                        })
                        .ToArray();

                    await blobFeedAction.PublishToFlatContainerAsync(blobs, MaxClients, pushOptions);

                    foreach (var package in buildModel.Artifacts.Blobs)
                    {
                        var assetRecord = buildInformation.Assets
                            .Where(a => a.Name.Equals(package.Id))
                            .SingleOrDefault();

                        if (assetRecord == null)
                        {
                            Log.LogError($"Asset with Id {package.Id} isn't registered on the BAR Build with ID {BARBuildId}");
                            continue;
                        }

                        await client.Assets.AddAssetLocationToAssetAsync(assetRecord.Id.Value, ExpectedFeedUrl, "NugetFeed");
                    }
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
