// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public static class BuildManifestUtil
    {
        public const string AssetsVirtualDir = "assets/";

        /// <summary>
        /// Create a build manifest for packages, blobs, and associated signing information
        /// </summary>
        /// <param name="log">MSBuild log helper</param>
        /// <param name="blobArtifacts">Collection of blobs</param>
        /// <param name="packageArtifacts">Collection of packages</param>
        /// <param name="assetManifestPath">Asset manifest file that should be written</param>
        /// <param name="manifestRepoName">Repository name</param>
        /// <param name="manifestBuildId">Azure devops build id</param>
        /// <param name="manifestBranch">Name of the branch that was built</param>
        /// <param name="manifestCommit">Commit that was built</param>
        /// <param name="manifestBuildData">Additional build data properties</param>
        /// <param name="isStableBuild">True if the build is stable, false otherwise.</param>
        /// <param name="publishingVersion">Publishing version in use.</param>
        /// <param name="isReleaseOnlyPackageVersion">True if this repo uses release-only package versions</param>
        /// <param name="signingInformationModel">Signing information.</param>
        public static void CreateBuildManifest(TaskLoggingHelper log,
            IEnumerable<BlobArtifactModel> blobArtifacts,
            IEnumerable<PackageArtifactModel> packageArtifacts,
            string assetManifestPath,
            string manifestRepoName,
            string manifestBuildId,
            string manifestBranch,
            string manifestCommit,
            string[] manifestBuildData,
            bool isStableBuild,
            PublishingInfraVersion publishingVersion,
            bool isReleaseOnlyPackageVersion,
            SigningInformationModel signingInformationModel = null)
        {
            CreateModel(
                blobArtifacts,
                packageArtifacts,
                manifestBuildId,
                manifestBuildData,
                manifestRepoName,
                manifestBranch,
                manifestCommit,
                isStableBuild,
                publishingVersion,
                isReleaseOnlyPackageVersion,
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
            ITaskItem[] certificatesSignInfo,
            string buildId,
            string[] manifestBuildData,
            string repoUri,
            string repoBranch,
            string repoCommit,
            bool isStableBuild,
            PublishingInfraVersion publishingVersion,
            bool isReleaseOnlyPackageVersion,
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

                    blobArtifacts.Add(BuildManifestUtil.CreateBlobArtifactModel(artifact, log));
                }
            }

            var signingInfoModel = CreateSigningInformationModelFromItems(
                itemsToSign, strongNameSignInfo, fileSignInfo, fileExtensionSignInfo,
                certificatesSignInfo, blobArtifacts, packageArtifacts, log);

            var buildModel = CreateModel(
                blobArtifacts,
                packageArtifacts,
                buildId,
                manifestBuildData,
                repoUri,
                repoBranch,
                repoCommit,
                isStableBuild,
                publishingVersion,
                isReleaseOnlyPackageVersion,
                log,
                signingInformationModel: signingInfoModel);
            return buildModel;
        }

        public static SigningInformationModel CreateSigningInformationModelFromItems(
            ITaskItem[] itemsToSign,
            ITaskItem[] strongNameSignInfo,
            ITaskItem[] fileSignInfo,
            ITaskItem[] fileExtensionSignInfo,
            ITaskItem[] certificatesSignInfo,
            IEnumerable<BlobArtifactModel> blobArtifacts,
            IEnumerable<PackageArtifactModel> packageArtifacts,
            TaskLoggingHelper log)
        {
            List<ItemToSignModel> parsedItemsToSign = new List<ItemToSignModel>();
            List<StrongNameSignInfoModel> parsedStrongNameSignInfo = new List<StrongNameSignInfoModel>();
            List<FileSignInfoModel> parsedFileSignInfo = new List<FileSignInfoModel>();
            List<FileExtensionSignInfoModel> parsedFileExtensionSignInfoModel = new List<FileExtensionSignInfoModel>();
            List<CertificatesSignInfoModel> parsedCertificatesSignInfoModel = new List<CertificatesSignInfoModel>();

            if (itemsToSign != null)
            {
                foreach (var itemToSign in itemsToSign)
                {
                    var fileName = Path.GetFileName(itemToSign.ItemSpec);
                    if (!blobArtifacts.Any(b => Path.GetFileName(b.Id).Equals(fileName, StringComparison.OrdinalIgnoreCase)) &&
                        !packageArtifacts.Any(p => $"{p.Id}.{p.Version}.nupkg".Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        log.LogError($"Item to sign '{itemToSign}' was not found in the artifacts");
                    }
                    parsedItemsToSign.Add(new ItemToSignModel { Include = Path.GetFileName(fileName) });
                }
            }
            if (strongNameSignInfo != null)
            {
                foreach (var signInfo in strongNameSignInfo)
                {
                    var attributes = signInfo.CloneCustomMetadata() as IDictionary<string, string>;
                    parsedStrongNameSignInfo.Add(new StrongNameSignInfoModel { Include = Path.GetFileName(signInfo.ItemSpec), CertificateName = attributes["CertificateName"], PublicKeyToken = attributes["PublicKeyToken"] });
                }
            }
            if (fileSignInfo != null)
            {
                foreach (var signInfo in fileSignInfo)
                {
                    var attributes = signInfo.CloneCustomMetadata() as IDictionary<string, string>;
                    parsedFileSignInfo.Add(new FileSignInfoModel { Include = Path.GetFileName(signInfo.ItemSpec), CertificateName = attributes["CertificateName"] });
                }
            }
            if (fileExtensionSignInfo != null)
            {
                foreach (var signInfo in fileExtensionSignInfo)
                {
                    var attributes = signInfo.CloneCustomMetadata() as IDictionary<string, string>;
                    parsedFileExtensionSignInfoModel.Add(new FileExtensionSignInfoModel { Include = signInfo.ItemSpec, CertificateName = attributes["CertificateName"] });
                }
            }
            if (certificatesSignInfo != null)
            {
                foreach (var signInfo in certificatesSignInfo)
                {
                    var attributes = signInfo.CloneCustomMetadata() as IDictionary<string, string>;
                    parsedCertificatesSignInfoModel.Add(new CertificatesSignInfoModel { Include = signInfo.ItemSpec, DualSigningAllowed = bool.Parse(attributes["DualSigningAllowed"]) });
                }
            }

            return new SigningInformationModel
            {
                ItemsToSign = parsedItemsToSign,
                StrongNameSignInfo = parsedStrongNameSignInfo,
                FileSignInfo = parsedFileSignInfo,
                FileExtensionSignInfo = parsedFileExtensionSignInfoModel,
                CertificatesSignInfo = parsedCertificatesSignInfoModel
            };
        }

        private static BuildModel CreateModel(IEnumerable<BlobArtifactModel> blobArtifacts,
            IEnumerable<PackageArtifactModel> packageArtifacts,
            string manifestBuildId,
            string[] manifestBuildData,
            string manifestRepoName,
            string manifestBranch,
            string manifestCommit,
            bool isStableBuild,
            PublishingInfraVersion publishingVersion,
            bool isReleaseOnlyPackageVersion,
            TaskLoggingHelper log,
            SigningInformationModel signingInformationModel = null)
        {
            var attributes = MSBuildListSplitter.GetNamedProperties(manifestBuildData);
            if (!ManifestBuildDataHasLocationInformation(attributes))
            {
                log.LogError("Missing 'location' property from ManifestBuildData");
            }
            BuildModel buildModel = new BuildModel(
                    new BuildIdentity
                    {
                        Attributes = attributes,
                        Name = manifestRepoName,
                        BuildId = manifestBuildId,
                        Branch = manifestBranch,
                        Commit = manifestCommit,
                        IsStable = isStableBuild,
                        PublishingVersion = publishingVersion,
                        IsReleaseOnlyPackageVersion = isReleaseOnlyPackageVersion
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

        public static BlobArtifactModel CreateBlobArtifactModel(ITaskItem item, TaskLoggingHelper log)
        {
            string path = item.GetMetadata("RelativeBlobPath");
            if (string.IsNullOrEmpty(path))
            {
                log.LogError($"Missing 'RelativeBlobPath' property on blob {item.ItemSpec}");
            }

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
