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

        public bool PublishFlatContainer { get; set; }

        public string ManifestRepoUri { get; set; }

        public string ManifestBuildId { get; set; } = "no build id provided";

        public string ManifestBranch { get; set; }

        public string ManifestCommit { get; set; }

        public string ManifestBuildData { get; set; }

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
                        ITaskItem[] symbolItems = ItemsToPush
                            .Where(i => i.ItemSpec.Contains("symbols.nupkg"))
                            .Select(i =>
                            {
                                string fileName = Path.GetFileName(i.ItemSpec);
                                i.SetMetadata("RelativeBlobPath", $"{BuildManifestUtil.AssetsVirtualDir}symbols/{fileName}");
                                return i;
                            })
                            .ToArray();

                        ITaskItem[] packageItems = ItemsToPush
                            .Where(i => !symbolItems.Contains(i))
                            .ToArray();

                        foreach (var packagePath in packageItems)
                        {
                            Log.LogMessage(MessageImportance.High, 
                                $"##vso[artifact.upload containerfolder=PackageArtifacts;artifactname=PackageArtifacts]{packagePath.ItemSpec}");
                        }

                        foreach (var symbolPath in symbolItems)
                        {
                            Log.LogMessage(MessageImportance.High, 
                                $"##vso[artifact.upload containerfolder=BlobArtifacts;artifactname=BlobArtifacts]{symbolPath.ItemSpec}");
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
