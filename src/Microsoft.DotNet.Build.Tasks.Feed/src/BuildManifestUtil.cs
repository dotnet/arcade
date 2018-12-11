// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public static class BuildManifestUtil
    {
        private static readonly char[] ManifestDataPairSeparators = { ';' };

        public const string AssetsVirtualDir = "assets/";

        public static void CreateBuildManifest(
            TaskLoggingHelper log,
            IEnumerable<BlobArtifactModel> blobArtifacts,
            IEnumerable<PackageArtifactModel> packageArtifacts,
            string assetManifestPath, string manifestRepoUri, string manifestBuildId,
            string manifestBranch, string manifestCommit, string manifestBuildData)
        {
            log.LogMessage(MessageImportance.High, $"Creating build manifest file '{assetManifestPath}'...");

            BuildModel buildModel = new BuildModel(
                    new BuildIdentity
                    {
                        Attributes = ParseManifestMetadataString(manifestBuildData),
                        Name = manifestRepoUri,
                        BuildId = manifestBuildId,
                        Branch = manifestBranch,
                        Commit = manifestCommit
                    });

            buildModel.Artifacts.Blobs.AddRange(blobArtifacts);
            buildModel.Artifacts.Packages.AddRange(packageArtifacts);

            string dirPath = Path.GetDirectoryName(assetManifestPath);

            Directory.CreateDirectory(dirPath);

            File.WriteAllText(assetManifestPath, buildModel.ToXml().ToString());
        }

        public static PackageArtifactModel CreatePackageArtifactModel(ITaskItem item)
        {
            NupkgInfo info = new NupkgInfo(item.ItemSpec);

            return new PackageArtifactModel
            {
                Attributes = ParseCustomAttributes(item),
                Id = info.Id,
                Version = info.Version
            };
        }

        public static BlobArtifactModel CreateBlobArtifactModel(ITaskItem item)
        {
            string path = item.GetMetadata("RelativeBlobPath");

            // Only include assets in the manifest if they're in "assets/".
            if (path?.StartsWith(AssetsVirtualDir, StringComparison.Ordinal) == true)
            {
                return new BlobArtifactModel
                {
                    Attributes = ParseCustomAttributes(item),
                    Id = path.Substring(AssetsVirtualDir.Length)
                };
            }
            return null;
        }

        private static Dictionary<string, string> ParseCustomAttributes(ITaskItem item)
        {
            return ParseManifestMetadataString(item.GetMetadata("ManifestArtifactData"));
        }

        private static Dictionary<string, string> ParseManifestMetadataString(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return new Dictionary<string, string>();
            }

            return data.Split(ManifestDataPairSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(pair =>
                {
                    int keyValueSeparatorIndex = pair.IndexOf('=');
                    if (keyValueSeparatorIndex > 0)
                    {
                        return new
                        {
                            Key = pair.Substring(0, keyValueSeparatorIndex).Trim(),
                            Value = pair.Substring(keyValueSeparatorIndex + 1).Trim()
                        };
                    }
                    return null;
                })
                .Where(pair => pair != null)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }
}
