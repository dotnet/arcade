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

                    if (PublishFlatContainer)
                    {
                        blobArtifacts = ItemsToPush.Select(BuildManifestUtil.CreateBlobArtifactModel).Where(blob => blob != null);
                    }
                    else
                    {
                        var itemsToPushNoExcludes = ItemsToPush.
                            Where(i => !string.Equals(i.GetMetadata("ExcludeFromManifest"), "true", StringComparison.OrdinalIgnoreCase));
                        ITaskItem[] symbolItems = itemsToPushNoExcludes
                            .Where(i => i.ItemSpec.Contains("symbols.nupkg"))
                            .Select(i =>
                            {
                                string fileName = Path.GetFileName(i.ItemSpec);
                                i.SetMetadata("RelativeBlobPath", $"{BuildManifestUtil.AssetsVirtualDir}symbols/{fileName}");
                                return i;
                            })
                            .ToArray();

                        ITaskItem[] packageItems = itemsToPushNoExcludes
                            .Where(i => !symbolItems.Contains(i))
                            .ToArray();

                        // To prevent conflicts with other parts of the build system that might move the artifacts
                        // folder while the artifacts are still being published, we copy the artifacts to a temporary
                        // location only for the sake of uploading them. This is a temporary solution and will be
                        // removed in the future.
                        if (!Directory.Exists(AssetsTemporaryDirectory))
                        {
                            Log.LogWarning($"Assets temporary directory {AssetsTemporaryDirectory} doesn't exist. Creating it.");
                            Directory.CreateDirectory(AssetsTemporaryDirectory);
                        }

                        foreach (var packagePath in packageItems)
                        {
                            var destFile = $"{AssetsTemporaryDirectory}/{Path.GetFileName(packagePath.ItemSpec)}";
                            File.Copy(packagePath.ItemSpec, destFile);

                            Log.LogMessage(MessageImportance.High,
                                $"##vso[artifact.upload containerfolder=PackageArtifacts;artifactname=PackageArtifacts]{destFile}");
                        }

                        foreach (var symbolPath in symbolItems)
                        {
                            var destFile = $"{AssetsTemporaryDirectory}/{Path.GetFileName(symbolPath.ItemSpec)}";
                            File.Copy(symbolPath.ItemSpec, destFile);

                            Log.LogMessage(MessageImportance.High,
                                $"##vso[artifact.upload containerfolder=BlobArtifacts;artifactname=BlobArtifacts]{destFile}");
                        }

                        packageArtifacts = packageItems.Select(BuildManifestUtil.CreatePackageArtifactModel);
                        blobArtifacts = symbolItems.Select(BuildManifestUtil.CreateBlobArtifactModel).Where(blob => blob != null);
                    }

                    BuildManifestUtil.CreateBuildManifest(Log, 
                        blobArtifacts, 
                        packageArtifacts,
                        AssetManifestPath, 
                        ManifestRepoUri, 
                        ManifestBuildId,
                        ManifestBranch, 
                        ManifestCommit, 
                        ManifestBuildData);
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
