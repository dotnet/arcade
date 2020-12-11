// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class PushToAzureDevOpsArtifacts : MSBuild.Task
    {
        [Required]
        public ITaskItem[] ItemsToPush { get; set; }

        public string AssetsTemporaryDirectory { get; set; }

        public bool PublishFlatContainer { get; set; }

        public string ManifestRepoName { get; set; }

        public string ManifestRepoUri { get; set; }

        public string ManifestBuildId { get; set; } = "no build id provided";

        public string ManifestBranch { get; set; }

        public string ManifestCommit { get; set; }

        public string[] ManifestBuildData { get; set; }

        public string AzureDevOpsCollectionUri { get; set; }

        public string AzureDevOpsProject { get; set; }

        public int AzureDevOpsBuildId { get; set; }

        public ITaskItem[] ItemsToSign { get; set; }

        public ITaskItem[] StrongNameSignInfo { get; set; }

        public ITaskItem[] FileSignInfo { get; set; }

        public ITaskItem[] FileExtensionSignInfo { get; set; }

        public ITaskItem[] CertificatesSignInfo { get; set; }

        public string AssetManifestPath { get; set; }

        public bool IsStableBuild { get; set; }

        public bool IsReleaseOnlyPackageVersion { get; set; }

        /// <summary>
        /// Which version should the build manifest be tagged with.
        /// By default he latest version is used.
        /// </summary>
        public string PublishingVersion { get; set; }

        public override bool Execute()
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Performing push to Azure DevOps artifacts storage.");

                if (!string.IsNullOrWhiteSpace(AssetsTemporaryDirectory))
                {
                    Log.LogMessage(MessageImportance.High, $"It's no longer necessary to specify a value for the {nameof(AssetsTemporaryDirectory)} property. " +
                        $"Please consider patching your code to not use it.");
                }

                if (ItemsToPush == null)
                {
                    Log.LogError($"No items to push. Please check ItemGroup ItemsToPush.");
                }
                else
                {
                    IEnumerable<BlobArtifactModel> blobArtifacts = Enumerable.Empty<BlobArtifactModel>();
                    IEnumerable<PackageArtifactModel> packageArtifacts = Enumerable.Empty<PackageArtifactModel>();

                    var itemsToPushNoExcludes = ItemsToPush.
                        Where(i => !string.Equals(i.GetMetadata("ExcludeFromManifest"), "true", StringComparison.OrdinalIgnoreCase));

                    if (PublishFlatContainer)
                    {
                        // Act as if %(PublishFlatContainer) were true for all items.
                        blobArtifacts = itemsToPushNoExcludes
                            .Select(i => BuildManifestUtil.CreateBlobArtifactModel(i, Log));
                        foreach (var blobItem in itemsToPushNoExcludes)
                        {
                            if (!File.Exists(blobItem.ItemSpec))
                            {
                                Log.LogError($"Could not find file {blobItem.ItemSpec}.");
                                continue;
                            }

                            Log.LogMessage(MessageImportance.High,
                                $"##vso[artifact.upload containerfolder=BlobArtifacts;artifactname=BlobArtifacts]{blobItem.ItemSpec}");
                        }
                    }
                    else
                    {
                        ITaskItem[] symbolItems = itemsToPushNoExcludes
                            .Where(i => i.ItemSpec.Contains("symbols.nupkg"))
                            .Select(i =>
                            {
                                string fileName = Path.GetFileName(i.ItemSpec);
                                i.SetMetadata("RelativeBlobPath", $"{BuildManifestUtil.AssetsVirtualDir}symbols/{fileName}");
                                return i;
                            })
                            .ToArray();

                        var blobItems = itemsToPushNoExcludes
                            .Where(i =>
                            {
                                var isFlatString = i.GetMetadata("PublishFlatContainer");
                                if (!string.IsNullOrEmpty(isFlatString) &&
                                    bool.TryParse(isFlatString, out var isFlat))
                                {
                                    return isFlat;
                                }

                                return false;
                            })
                            .Union(symbolItems)
                            .ToArray();

                        ITaskItem[] packageItems = itemsToPushNoExcludes
                            .Except(blobItems)
                            .ToArray();

                        foreach (var packagePath in packageItems)
                        {
                            if (!File.Exists(packagePath.ItemSpec))
                            {
                                Log.LogError($"Could not find file {packagePath.ItemSpec}.");
                                continue;
                            }

                            Log.LogMessage(MessageImportance.High,
                                $"##vso[artifact.upload containerfolder=PackageArtifacts;artifactname=PackageArtifacts]{packagePath.ItemSpec}");
                        }

                        foreach (var blobItem in blobItems)
                        {
                            if (!File.Exists(blobItem.ItemSpec))
                            {
                                Log.LogError($"Could not find file {blobItem.ItemSpec}.");
                                continue;
                            }

                            Log.LogMessage(MessageImportance.High,
                                $"##vso[artifact.upload containerfolder=BlobArtifacts;artifactname=BlobArtifacts]{blobItem.ItemSpec}");
                        }

                        packageArtifacts = packageItems.Select(BuildManifestUtil.CreatePackageArtifactModel);
                        blobArtifacts = blobItems.Select(i => BuildManifestUtil.CreateBlobArtifactModel(i, Log)).Where(blob => blob != null);
                    }

                    PublishingInfraVersion targetPublishingVersion = PublishingInfraVersion.Latest;

                    if (!string.IsNullOrEmpty(PublishingVersion))
                    {
                        if (!Enum.TryParse(PublishingVersion, ignoreCase: true, out targetPublishingVersion))
                        {
                            Log.LogError($"Could not parse publishing infra version '{PublishingVersion}'");
                        }
                    }
                    
                    SigningInformationModel signingInformationModel = BuildManifestUtil.CreateSigningInformationModelFromItems(
                        ItemsToSign, StrongNameSignInfo, FileSignInfo, FileExtensionSignInfo, CertificatesSignInfo, blobArtifacts, packageArtifacts, Log);

                    BuildManifestUtil.CreateBuildManifest(Log,
                        blobArtifacts,
                        packageArtifacts,
                        AssetManifestPath,
                        !String.IsNullOrEmpty(ManifestRepoName) ? ManifestRepoName : ManifestRepoUri,
                        ManifestBuildId,
                        ManifestBranch,
                        ManifestCommit,
                        ManifestBuildData,
                        IsStableBuild,
                        targetPublishingVersion,
                        IsReleaseOnlyPackageVersion,
                        signingInformationModel: signingInformationModel);

                    Log.LogMessage(MessageImportance.High,
                        $"##vso[artifact.upload containerfolder=AssetManifests;artifactname=AssetManifests]{AssetManifestPath}");
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
