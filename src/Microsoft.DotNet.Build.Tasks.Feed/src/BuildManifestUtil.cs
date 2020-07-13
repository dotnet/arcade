// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
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
            PublishingInfraVersion publishingVersion,
            SigningInformationModel signingInformationModel = null)
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
                publishingVersion,
                log,
                signingInformationModel: signingInformationModel)
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
            ITaskItem[] itemsToSign,
            ITaskItem[] strongNameSignInfo,
            ITaskItem[] fileSignInfo,
            ITaskItem[] fileExtensionSignInfo,
            string buildId,
            string[] BuildProperties,
            string repoUri,
            string repoBranch,
            string repoCommit,
            bool isStableBuild,
            PublishingInfraVersion publishingVersion,
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

            var buildModel = CreateModel(
                blobArtifacts,
                packageArtifacts,
                buildId,
                BuildProperties,
                repoUri,
                repoBranch,
                repoCommit,
                isStableBuild,
                publishingVersion,
                log,
                signingInformationModel: CreateSigningInformationModelFromItems(itemsToSign, strongNameSignInfo, fileSignInfo, fileExtensionSignInfo));
            return buildModel;
        }

        public static SigningInformationModel CreateSigningInformationModelFromItems(ITaskItem[] itemsToSign, ITaskItem[] strongNameSignInfo, ITaskItem[] fileSignInfo, ITaskItem[] fileExtensionSignInfo)
        {
            List<ItemToSignModel> parsedItemsToSign = new List<ItemToSignModel>();
            List<StrongNameSignInfoModel> parsedStrongNameSignInfo = new List<StrongNameSignInfoModel>();
            List<FileSignInfoModel> parsedFileSignInfo = new List<FileSignInfoModel>();
            List<FileExtensionSignInfoModel> parsedFileExtensionSignInfoModel = new List<FileExtensionSignInfoModel>();

            if (itemsToSign != null)
            {
                foreach (var itemToSign in itemsToSign)
                {
                    var filename = itemToSign.ItemSpec.Replace('\\', '/');
                    {
                        parsedItemsToSign.Add(new ItemToSignModel { File = Path.GetFileName(filename) });
                    }
                }
            }
            if (strongNameSignInfo != null)
            {
                foreach (var signInfo in strongNameSignInfo)
                {
                    var attributes = signInfo.CloneCustomMetadata() as Dictionary<string, string>;
                    parsedStrongNameSignInfo.Add(new StrongNameSignInfoModel { File = Path.GetFileName(signInfo.ItemSpec), CertificateName = attributes["CertificateName"], PublicKeyToken = attributes["PublicKeyToken"] });
                }
            }
            if (fileSignInfo != null)
            {
                foreach (var signInfo in fileSignInfo)
                {
                    var attributes = signInfo.CloneCustomMetadata() as Dictionary<string, string>;
                    parsedFileSignInfo.Add(new FileSignInfoModel { File = signInfo.ItemSpec, CertificateName = attributes["CertificateName"] });
                }
            }
            if (fileExtensionSignInfo != null)
            {
                foreach (var signInfo in fileExtensionSignInfo)
                {
                    var attributes = signInfo.CloneCustomMetadata() as Dictionary<string, string>;
                    parsedFileExtensionSignInfoModel.Add(new FileExtensionSignInfoModel { Extension = signInfo.ItemSpec, CertificateName = attributes["CertificateName"] });
                }
            }

            return new SigningInformationModel
            {
                ItemsToSign = parsedItemsToSign,
                StrongNameSignInfo = parsedStrongNameSignInfo,
                FileSignInfo = parsedFileSignInfo,
                FileExtensionSignInfo = parsedFileExtensionSignInfoModel
            };
        }

        private static BuildModel CreateModel(IEnumerable<BlobArtifactModel> blobArtifacts,
            IEnumerable<PackageArtifactModel> packageArtifacts,
            string manifestBuildId,
            string[] manifestBuildData,
            string manifestRepoUri,
            string manifestBranch,
            string manifestCommit,
            bool isStableBuild,
            PublishingInfraVersion publishingVersion,
            TaskLoggingHelper log,
            SigningInformationModel signingInformationModel = null)
        {
            var attributes = MSBuildListSplitter.GetNamedProperties(manifestBuildData);
            if (!ManifestBuildDataHasLocationInformation(attributes))
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
                        PublishingVersion = publishingVersion
                    });

            buildModel.Artifacts.Blobs.AddRange(blobArtifacts);
            buildModel.Artifacts.Packages.AddRange(packageArtifacts);
            buildModel.SigningInformation = signingInformationModel;
            return buildModel;
        }

        internal static bool ManifestBuildDataHasLocationInformation(string[] manifestBuildData)
        {
            return ManifestBuildDataHasLocationInformation(MSBuildListSplitter.GetNamedProperties(manifestBuildData));
        }

        internal static bool ManifestBuildDataHasLocationInformation(IDictionary<string, string> attributes)
        {
            return attributes.ContainsKey("Location") || attributes.ContainsKey("InitialAssetsLocation");
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
