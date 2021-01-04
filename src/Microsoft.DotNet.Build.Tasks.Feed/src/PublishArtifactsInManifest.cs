// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Maestro.Client;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    /// <summary>
    ///     The intended use of this task is to push artifacts described in
    ///     a build manifest to package feeds.
    /// </summary>
    public class PublishArtifactsInManifest : MSBuildTaskBase
    {
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
        public bool InternalBuild { get; set; }

        public bool PublishInstallersAndChecksums { get; set; } = false;

        public string AzureStorageTargetFeedKey { get; set; }

        public string ChecksumsFeedKey { get; set; }

        public string InstallersFeedKey { get; set; }

        public string InternalInstallersFeedKey { get; set; }

        public string InternalCheckSumsFeedKey { get; set; }

        public string AzureDevOpsFeedsKey { get; set; }

        /// <summary>
        /// Path to dll and pdb files
        /// </summary>
        public string PdbArtifactsBasePath {get; set;}

        /// <summary>
        /// Token to publish to Msdl symbol server
        /// </summary>
        public string MsdlToken {get; set;}

        /// <summary>
        /// Token to publish to SymWeb symbol server 
        /// </summary>
        public string SymWebToken {get; set;}

        /// <summary>
        /// Files to exclude from symbol publishing
        /// </summary>
        public string SymbolPublishingExclusionsFile {get; set;}

        /// <summary>
        /// 
        /// </summary>
        public bool PublishSpecialClrFiles { get; set; }

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

        /// <summary>
        /// Just an internal flag to keep track whether we published assets via a V3 manifest or not.
        /// </summary>
        private static bool PublishedV3Manifest { get; set; }

        private IBuildModelFactory _buildModelFactory;
        private IFileSystem _fileSystem;

        public override void ConfigureServices(IServiceCollection collection)
        {
            collection.TryAddSingleton<IBuildModelFactory, BuildModelFactory>();
            collection.TryAddSingleton<ISigningInformationModelFactory, SigningInformationModelFactory>();
            collection.TryAddSingleton<IBlobArtifactModelFactory, BlobArtifactModelFactory>();
            collection.TryAddSingleton<IPackageArtifactModelFactory, PackageArtifactModelFactory>();
            collection.TryAddSingleton<INupkgInfoFactory, NupkgInfoFactory>();
            collection.TryAddSingleton<IPackageArchiveReaderFactory, PackageArchiveReaderFactory>();
            collection.TryAddSingleton<IFileSystem, FileSystem>();
            collection.TryAddSingleton(Log);
        }

        public bool ExecuteTask(IBuildModelFactory buildModelFactory,
            IFileSystem fileSystem)
        {
            _buildModelFactory = buildModelFactory;
            _fileSystem = fileSystem;

            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                var tasks = AssetManifestPaths
                    .Select(manifestParam => WhichPublishingTask(manifestParam.ItemSpec))
                    .ToArray();

                // Check that was possible to construct a publishing task for all manifests
                if (tasks.Any(t => t == null))
                {
                    return false;
                }

                // Process all manifests in parallel
                var results = await Task.WhenAll(
                    tasks.Select(t => t.ExecuteAsync())
                );

                // Check that all tasks returned true
                if (results.All(t => t))
                {
                    // Currently a build can produce several build manifests and publish them independently.
                    // It's also possible that somehow we have manifests using different versions of the publishing infra.
                    //
                    // The V3 infra, once all assets have been published, promotes the build to the target channels informed. 
                    // Since we can have multiple manifests (perhaps using different versions), things
                    // get a bit more complicated. For now, we are going to just promote the build to the target 
                    // channels if it creates at least one V3 manifest.
                    //
                    // There is an issue to merge all build manifests into a single one before publishing:
                    //         https://github.com/dotnet/arcade/issues/5489
                    if (PublishedV3Manifest)
                    {
                        IMaestroApi client = ApiFactory.GetAuthenticated(MaestroApiEndpoint, BuildAssetRegistryToken);
                        Maestro.Client.Models.Build buildInformation = await client.Builds.GetBuildAsync(BARBuildId);

                        var targetChannelsIds = TargetChannels.Split(',').Select(ci => int.Parse(ci));

                        foreach (var targetChannelId in targetChannelsIds)
                        {
                            await client.Channels.AddBuildToChannelAsync(BARBuildId, targetChannelId);
                        }
                    }

                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
                return false;
            }
        }

        public PublishArtifactsInManifestBase WhichPublishingTask(string manifestFullPath)
        {
            Log.LogMessage(MessageImportance.High, $"Creating a task to publish assets from {manifestFullPath}");

            if (!_fileSystem.FileExists(manifestFullPath))
            {
                Log.LogError($"Problem reading asset manifest path from '{manifestFullPath}'");
                return null;
            }

            BuildModel buildModel = _buildModelFactory.ManifestFileToModel(manifestFullPath);
            
            if (buildModel.Identity.PublishingVersion == PublishingInfraVersion.Legacy)
            {
                Log.LogError("This task is not able to handle legacy manifests.");
                return null;
            }
            else if (buildModel.Identity.PublishingVersion == PublishingInfraVersion.Latest)
            {
                return ConstructPublishingV2Task(buildModel);
            }
            else if (buildModel.Identity.PublishingVersion == PublishingInfraVersion.Next)
            {
                return ConstructPublishingV3Task(buildModel);
            }
            else
            {
                Log.LogError($"The manifest version '{buildModel.Identity.PublishingVersion}' is not recognized by the publishing task.");
                return null;
            }
        }

        internal PublishArtifactsInManifestBase ConstructPublishingV2Task(BuildModel buildModel)
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
            };
        }

        internal PublishArtifactsInManifestBase ConstructPublishingV3Task(BuildModel buildModel)
        {
            PublishedV3Manifest = true;

            return new PublishArtifactsInManifestV3()
            {
                BuildEngine = this.BuildEngine,
                TargetChannels = this.TargetChannels,
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
                PublishInstallersAndChecksums = this.PublishInstallersAndChecksums,
                AzureDevOpsFeedsKey = this.AzureDevOpsFeedsKey,
                InstallersFeedKey = this.InstallersFeedKey,
                CheckSumsFeedKey = this.ChecksumsFeedKey,
                InternalCheckSumsFeedKey = this.InternalCheckSumsFeedKey,
                InternalInstallersFeedKey = this.InternalInstallersFeedKey,
                AzureStorageTargetFeedKey = this.AzureStorageTargetFeedKey,
                PdbArtifactsBasePath = this.PdbArtifactsBasePath,
                SymWebToken = this.SymWebToken,
                MsdlToken = this.MsdlToken,
                SymbolPublishingExclusionsFile = this.SymbolPublishingExclusionsFile,
                PublishSpecialClrFiles = this.PublishSpecialClrFiles
            };
        }
    }
}
