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
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public static class BuildManifestUtil
    {
        public const string AssetsVirtualDir = "assets/";

        public static void CreateBuildManifest(TaskLoggingHelper log,
            IEnumerable<BlobArtifactModel> blobArtifacts,
            IEnumerable<PackageArtifactModel> packageArtifacts,
            string assetManifestPath,
            string manifestRepoUri,
            string manifestBuildId,
            string manifestBranch,
            string manifestCommit,
            string[] manifestBuildData,
            bool isStableBuild,
            bool publishToFlatContainer)
        {
            CreateModel(
                blobArtifacts,
                packageArtifacts,
                manifestBuildId,
                manifestBuildData,
                manifestRepoUri,
                manifestBranch,
                manifestCommit,
                isStableBuild,
                publishToFlatContainer,
                log)
                .WriteAsXml(assetManifestPath, log);
        }

        public static void WriteAsXml(this BuildModel buildModel, string filePath, TaskLoggingHelper log)
        {
            log.LogMessage(MessageImportance.High, $"Creating build manifest file '{filePath}'...");
            string dirPath = Path.GetDirectoryName(filePath);

            Directory.CreateDirectory(dirPath);

            File.WriteAllText(filePath, buildModel.ToXml().ToString());
        }

        public static BuildModel CreateModelFromItems(
            ITaskItem[] artifacts,
            string buildId,
            string[] BuildProperties,
            string repoUri,
            string repoBranch,
            string repoCommit,
            bool isStableBuild,
            bool publishToFlatContainer,
            TaskLoggingHelper log)
        {
            if (artifacts == null)
            {
                throw new ArgumentNullException(nameof(artifacts));
            }

            var blobArtifacts = new List<BlobArtifactModel>();
            var packageArtifacts = new List<PackageArtifactModel>();

            foreach (var artifact in artifacts)
            {
                if (string.Equals(artifact.GetMetadata("ExcludeFromManifest"), "true", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var isSymbolsPackage = artifact.ItemSpec.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase)
                    || artifact.ItemSpec.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase);

                if (artifact.ItemSpec.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase) && !isSymbolsPackage)
                {
                    packageArtifacts.Add(BuildManifestUtil.CreatePackageArtifactModel(artifact));
                }
                else
                {
                    if (isSymbolsPackage)
                    {
                        string fileName = Path.GetFileName(artifact.ItemSpec);
                        artifact.SetMetadata("RelativeBlobPath", $"{BuildManifestUtil.AssetsVirtualDir}symbols/{fileName}");
                    }

                    blobArtifacts.Add(BuildManifestUtil.CreateBlobArtifactModel(artifact));
                }
            }

            var buildModel = BuildManifestUtil.CreateModel(
                blobArtifacts,
                packageArtifacts,
                buildId,
                BuildProperties,
                repoUri,
                repoBranch,
                repoCommit,
                isStableBuild,
                publishToFlatContainer,
                log);
            return buildModel;
        }

        private static BuildModel CreateModel(IEnumerable<BlobArtifactModel> blobArtifacts,
            IEnumerable<PackageArtifactModel> packageArtifacts,
            string manifestBuildId,
            string[] manifestBuildData,
            string manifestRepoUri,
            string manifestBranch,
            string manifestCommit,
            bool isStableBuild,
            bool publishToFlatContainer,
            TaskLoggingHelper log)
        {
            var attributes = MSBuildListSplitter.GetNamedProperties(manifestBuildData);
            if(!ManifestBuildDataHasLocationProperty(attributes))
            {
                log.LogError($"Missing 'location' property from ManifestBuildData");
            }
            BuildModel buildModel = new BuildModel(
                    new BuildIdentity
                    {
                        Attributes = attributes,
                        Name = manifestRepoUri,
                        BuildId = manifestBuildId,
                        Branch = manifestBranch,
                        Commit = manifestCommit,
                        IsStable = isStableBuild.ToString(),
                        PublishToFlatContainer = publishToFlatContainer.ToString()
                    });

            buildModel.Artifacts.Blobs.AddRange(blobArtifacts);
            buildModel.Artifacts.Packages.AddRange(packageArtifacts);
            return buildModel;
        }

        internal static bool ManifestBuildDataHasLocationProperty(string [] manifestBuildData)
        {
            return ManifestBuildDataHasLocationProperty(MSBuildListSplitter.GetNamedProperties(manifestBuildData));
        }

        internal static bool ManifestBuildDataHasLocationProperty(IDictionary<string, string> attributes)
        {
            return attributes.ContainsKey("Location");
        }

        public static BuildModel ManifestFileToModel(string assetManifestPath, TaskLoggingHelper log)
        {
            try
            {
                return BuildModel.Parse(XElement.Load(assetManifestPath));
            }
            catch (Exception e)
            {
                log.LogError($"Could not parse asset manifest file: {assetManifestPath}");
                log.LogErrorFromException(e);
                return null;
            }
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

            return new BlobArtifactModel
            {
                Attributes = ParseCustomAttributes(item),
                Id = path
            };
        }

        private static IDictionary<string, string> ParseCustomAttributes(ITaskItem item)
        {
            return MSBuildListSplitter.GetNamedProperties(item.GetMetadata("ManifestArtifactData"));
        }
    }
}
