// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        [Required]
        public string AssetsTemporaryDirectory { get; set; }

        public bool PublishFlatContainer { get; set; }

        public string ManifestRepoUri { get; set; }

        public string ManifestBuildId { get; set; } = "no build id provided";

        public string ManifestBranch { get; set; }

        public string ManifestCommit { get; set; }

        public string[] ManifestBuildData { get; set; }

        public string AssetManifestPath { get; set; }

        public bool IsStableBuild { get; set; }

        public override bool Execute()
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Performing push to Azure DevOps artifacts storage.");

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

                    // To prevent conflicts with other parts of the build system that might move the artifacts
                    // folder while the artifacts are still being published, we copy the artifacts to a temporary
                    // location only for the sake of uploading them. This is a temporary solution and will be
                    // removed in the future.
                    if (!Directory.Exists(AssetsTemporaryDirectory))
                    {
                        Log.LogMessage(MessageImportance.High,
                            $"Assets temporary directory {AssetsTemporaryDirectory} doesn't exist. Creating it.");
                        Directory.CreateDirectory(AssetsTemporaryDirectory);
                    }

                    if (PublishFlatContainer)
                    {
                        // Act as if %(PublishFlatContainer) were true for all items.
                        blobArtifacts = itemsToPushNoExcludes
                            .Select(BuildManifestUtil.CreateBlobArtifactModel);
                        foreach (var blobItem in itemsToPushNoExcludes)
                        {
                            var destFile = $"{AssetsTemporaryDirectory}/{Path.GetFileName(blobItem.ItemSpec)}";
                            File.Copy(blobItem.ItemSpec, destFile);
                            Log.LogMessage(MessageImportance.High,
                                $"##vso[artifact.upload containerfolder=BlobArtifacts;artifactname=BlobArtifacts]{destFile}");
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
                            var destFile = $"{AssetsTemporaryDirectory}/{Path.GetFileName(packagePath.ItemSpec)}";
                            File.Copy(packagePath.ItemSpec, destFile);

                            Log.LogMessage(MessageImportance.High,
                                $"##vso[artifact.upload containerfolder=PackageArtifacts;artifactname=PackageArtifacts]{destFile}");
                        }

                        foreach (var blobItem in blobItems)
                        {
                            var destFile = $"{AssetsTemporaryDirectory}/{Path.GetFileName(blobItem.ItemSpec)}";
                            File.Copy(blobItem.ItemSpec, destFile);

                            Log.LogMessage(MessageImportance.High,
                                $"##vso[artifact.upload containerfolder=BlobArtifacts;artifactname=BlobArtifacts]{destFile}");
                        }

                        packageArtifacts = packageItems.Select(BuildManifestUtil.CreatePackageArtifactModel);
                        blobArtifacts = blobItems.Select(BuildManifestUtil.CreateBlobArtifactModel).Where(blob => blob != null);
                    }

                    BuildManifestUtil.CreateBuildManifest(Log,
                        blobArtifacts,
                        packageArtifacts,
                        AssetManifestPath,
                        ManifestRepoUri,
                        ManifestBuildId,
                        ManifestBranch,
                        ManifestCommit,
                        ManifestBuildData,
                        IsStableBuild);
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
