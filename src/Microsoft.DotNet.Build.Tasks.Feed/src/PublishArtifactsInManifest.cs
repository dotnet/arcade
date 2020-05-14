// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    /// <summary>
    ///     The intended use of this task is to push artifacts described in
    ///     a build manifest to a static package feed.
    /// </summary>
    public class PublishArtifactsInManifest : Microsoft.Build.Utilities.Task
    {
        #region MSBuild Task Parameters
        /// <summary>
        /// Comma separated list of Maestro++ Channel IDs to which the build should
        /// be assigned to once the assets are published.
        /// </summary>
        public string TargetChannels { get; set; }

        /// <summary>
        /// Configuration telling which target feed to use for each artifact category.
        /// ItemSpec: ArtifactCategory
        /// Metadata TargetURL: target URL where assets of this category should be published to.
        /// Metadata Type: type of the target feed.
        /// Metadata Token: token to be used for publishing to target feed.
        /// Metadata AssetSelection (optional): Can be "All", "ShippingOnly" or "NonShippingOnly"
        ///                                     Determines which assets are pushed to this feed config
        /// Metadata Internal (optional): If true, the feed is only internally accessible.
        ///                               If false, the feed is publicly visible and internal builds wwill be rejected.
        ///                               If not provided, then this task will attempt to determine whether the feed URL is publicly visible or not.
        ///                               Unless SkipSafetyChecks is passed, the publishing infrastructure will check the accessibility of the feed.
        /// Metadata Isolated (optional): If true, stable packages can be pushed to this feed.
        ///                               If false, stable packages will be rejected.
        ///                               If not provided then defaults to false.
        /// Metadata AllowOverwrite (optional): If true, existing azure blob storage assets can be overwritten
        ///                                     If false, an error is thrown if an asset already exists
        ///                                     If not provided then defaults to false.
        ///                                     Azure DevOps feeds can never be overwritten.
        /// Metadata LatestLinkShortUrlPrefix (optional): If provided, AKA ms links are generated (for artifacts blobs only)
        ///                                               that target this short url path. The link is construct as such:
        ///                                               aka.ms/AkaShortUrlPath/BlobArtifactPath -> Target blob url
        ///                                               If specified, then AkaMSClientId, AkaMSClientSecret and AkaMSTenant must be provided.
        ///                                               The version information is stripped away from the file and blob artifact path.
        /// </summary>
        public ITaskItem[] TargetFeedConfig { get; set; }

        /// <summary>
        /// Full path to the assets to publish manifest(s)
        /// </summary>
        [Required]
        public ITaskItem[] AssetManifestPaths { get; set; }

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
        /// Maximum number of parallel uploads for the upload tasks
        /// </summary>
        public int MaxClients { get; set; } = 16;

        /// <summary>
        /// Directory where "nuget.exe" is installed. This will be used to publish packages.
        /// </summary>
        [Required]
        public string NugetPath { get; set; }

        /// <summary>
        /// Whether this build is internal or not. If true, extra checks are done to avoid accidental
        /// publishing of assets to public feeds or storage accounts.
        /// </summary>
        [Required]
        public bool InternalBuild { get; set; }

        public string ArtifactsCategory { get; set; }

        public string ChecksumsFeedKey { get; set; }

        public string InstallersFeedKey { get; set; }

        public string AzureStorageTargetFeedKey { get; set; }

        public string AzureDevOpsFeedsKey { get; set; }

        /// <summary>
        /// If true, safety checks only print messages and do not error
        /// - Internal asset to public feed
        /// - Stable packages to non-isolated feeds
        /// </summary>
        public bool SkipSafetyChecks { get; set; } = false;

        public string AkaMSClientId { get; set; }

        public string AkaMSClientSecret { get; set; }

        public string AkaMSTenant { get; set; }

        public string AkaMsOwners { get; set; }

        public string AkaMSCreatedBy { get; set; }

        public string AkaMSGroupOwner { get; set; }
        #endregion

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                var tasks = AssetManifestPaths
                            .Select(manifestParam => WhichPublishingTask(manifestParam.ItemSpec))
                            .ToArray();

                // Wait for tasks in parallel
                await Task.WhenAll(tasks);

                // Make sure all tasks returned true
                return tasks.All(t => t.Result);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
                return false;
            }
        }

        internal Task<bool> WhichPublishingTask(string manifestFullPath)
        {
            Log.LogMessage(MessageImportance.High, $"Reading manifest from {manifestFullPath}");

            if (!File.Exists(manifestFullPath))
            {
                Log.LogError($"Problem reading asset manifest path from '{manifestFullPath}'");
                return Task.FromResult(false);
            }

            BuildModel buildModel = BuildManifestUtil.ManifestFileToModel(manifestFullPath, Log);
            
            if (buildModel.Identity.PublishingVersion == PublishingInfraVersion.Legacy)
            {
                Log.LogError("This task is not able to handle legacy manifests.");
                return Task.FromResult(false);
            }
            else if (buildModel.Identity.PublishingVersion == PublishingInfraVersion.Latest)
            {
                return ConstructLatestPublishingTask(buildModel);
            }
            else if (buildModel.Identity.PublishingVersion == PublishingInfraVersion.Next)
            {
                return ConstructNextPublishingTask(buildModel);
            }
            else
            {
                Log.LogError($"The manifest version '{buildModel.Identity.PublishingVersion}' is not recognized by the publishing task.");
                return Task.FromResult(false);
            }
        }

        internal Task<bool> ConstructLatestPublishingTask(BuildModel buildModel)
        {
            return new PublishArtifactsInManifestV2()
            {
                BuildEngine = this.BuildEngine,
                TargetFeedConfig = this.TargetFeedConfig,
                BuildModel = buildModel,
                BlobAssetsBasePath = this.BlobAssetsBasePath,
                PackageAssetsBasePath = this.PackageAssetsBasePath,
                BARBuildId = this.BARBuildId,
                MaestroApiEndpoint = this.MaestroApiEndpoint,
                BuildAssetRegistryToken = this.BuildAssetRegistryToken,
                MaxClients = this.MaxClients,
                NugetPath = this.NugetPath,
                InternalBuild = this.InternalBuild,
                SkipSafetyChecks = this.SkipSafetyChecks,
                AkaMSClientId = this.AkaMSClientId,
                AkaMSClientSecret = this.AkaMSClientSecret,
                AkaMSCreatedBy = this.AkaMSCreatedBy,
                AkaMSGroupOwner = this.AkaMSGroupOwner,
                AkaMsOwners = this.AkaMsOwners,
                AkaMSTenant = this.AkaMSTenant
            }.ExecuteAsync();
        }

        internal Task<bool> ConstructNextPublishingTask(BuildModel buildModel)
        {
            return new PublishArtifactsInManifestV3()
            {
                BuildEngine = this.BuildEngine,
                TargetChannels = this.TargetChannels,
                ArtifactsCategory = this.ArtifactsCategory,
                BuildModel = buildModel,
                BlobAssetsBasePath = this.BlobAssetsBasePath,
                PackageAssetsBasePath = this.PackageAssetsBasePath,
                BARBuildId = this.BARBuildId,
                MaestroApiEndpoint = this.MaestroApiEndpoint,
                BuildAssetRegistryToken = this.BuildAssetRegistryToken,
                MaxClients = this.MaxClients,
                NugetPath = this.NugetPath,
                InternalBuild = this.InternalBuild,
                SkipSafetyChecks = this.SkipSafetyChecks,
                AkaMSClientId = this.AkaMSClientId,
                AkaMSClientSecret = this.AkaMSClientSecret,
                AkaMSCreatedBy = this.AkaMSCreatedBy,
                AkaMSGroupOwner = this.AkaMSGroupOwner,
                AkaMsOwners = this.AkaMsOwners,
                AkaMSTenant = this.AkaMSTenant,
                AzureStorageTargetFeedKey = this.AzureStorageTargetFeedKey,
                AzureDevOpsFeedsKey = this.AzureDevOpsFeedsKey,
                InstallersFeedKey = this.InstallersFeedKey,
                CheckSumsFeedKey = this.ChecksumsFeedKey,
            }.ExecuteAsync();
        }
    }
}
