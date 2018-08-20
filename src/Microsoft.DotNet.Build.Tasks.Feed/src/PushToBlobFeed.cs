// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class PushToBlobFeed : MSBuild.Task
    {
        private static readonly char[] ManifestDataPairSeparators = { ';' };
        private const string AssetsVirtualDir = "assets/";

        [Required]
        public string ExpectedFeedUrl { get; set; }

        [Required]
        public string AccountKey { get; set; }

        [Required]
        public ITaskItem[] ItemsToPush { get; set; }

        public bool Overwrite { get; set; }

        /// <summary>
        /// Enables idempotency when Overwrite is false.
        /// 
        /// false: (default) Attempting to upload an item that already exists fails.
        /// 
        /// true: When an item already exists, download the existing blob to check if it's
        /// byte-for-byte identical to the one being uploaded. If so, pass. If not, fail.
        /// </summary>
        public bool PassIfExistingItemIdentical { get; set; }

        public bool PublishFlatContainer { get; set; }

        public int MaxClients { get; set; } = 8;

        public bool SkipCreateContainer { get; set; } = false;

        public int UploadTimeoutInMinutes { get; set; } = 5;

        public string ManifestRepoUri { get; set; }

        public string ManifestBuildId { get; set; } = "no build id provided";

        public string ManifestBranch { get; set; }

        public string ManifestCommit { get; set; }

        public string ManifestBuildData { get; set; }

        public string AssetManifestPath { get; set; }

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Performing feed push...");

                if (ItemsToPush == null)
                {
                    Log.LogError($"No items to push. Please check ItemGroup ItemsToPush.");
                }
                else
                {
                    BlobFeedAction blobFeedAction = new BlobFeedAction(ExpectedFeedUrl, AccountKey, Log);

                    IEnumerable<BlobArtifactModel> blobArtifacts = Enumerable.Empty<BlobArtifactModel>();
                    IEnumerable<PackageArtifactModel> packageArtifacts = Enumerable.Empty<PackageArtifactModel>();

                    if (!SkipCreateContainer)
                    {
                        await blobFeedAction.CreateContainerAsync(BuildEngine, PublishFlatContainer);
                    }

                    if (PublishFlatContainer)
                    {
                        await PublishToFlatContainerAsync(ItemsToPush, blobFeedAction);
                        blobArtifacts = ConcatBlobArtifacts(blobArtifacts, ItemsToPush);
                    }
                    else
                    {
                        ITaskItem[] symbolItems = ItemsToPush
                            .Where(i => i.ItemSpec.Contains("symbols.nupkg"))
                            .Select(i =>
                            {
                                string fileName = Path.GetFileName(i.ItemSpec);
                                i.SetMetadata("RelativeBlobPath", $"{AssetsVirtualDir}symbols/{fileName}");
                                return i;
                            })
                            .ToArray();

                        ITaskItem[] packageItems = ItemsToPush
                            .Where(i => !symbolItems.Contains(i))
                            .ToArray();

                        var packagePaths = packageItems.Select(i => i.ItemSpec);

                        await blobFeedAction.PushToFeedAsync(packagePaths, CreatePushOptions());
                        await PublishToFlatContainerAsync(symbolItems, blobFeedAction);

                        packageArtifacts = ConcatPackageArtifacts(packageArtifacts, packageItems);
                        blobArtifacts = ConcatBlobArtifacts(blobArtifacts, symbolItems);
                    }

                    CreateBuildManifest(AssetManifestPath, blobArtifacts, packageArtifacts);
                }
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }

        private void CreateBuildManifest(
            string manifestPath,
            IEnumerable<BlobArtifactModel> blobArtifacts,
            IEnumerable<PackageArtifactModel> packageArtifacts)
        {
            Log.LogMessage($"Creating build manifest file '{AssetManifestPath}'...");

            BuildModel buildModel = new BuildModel(
                    new BuildIdentity
                    {
                        Attributes = ParseManifestMetadataString(ManifestBuildData),
                        Name = ManifestRepoUri,
                        BuildId = ManifestBuildId,
                        Branch = ManifestBranch,
                        Commit = ManifestCommit
                    });

            buildModel.Artifacts.Blobs.AddRange(blobArtifacts);
            buildModel.Artifacts.Packages.AddRange(packageArtifacts);

            string dirPath = Path.GetDirectoryName(AssetManifestPath);

            Directory.CreateDirectory(dirPath);

            File.WriteAllText(AssetManifestPath, buildModel.ToXml().ToString());
        }

        private async Task PublishToFlatContainerAsync(IEnumerable<ITaskItem> taskItems, BlobFeedAction blobFeedAction)
        {
            if (taskItems.Any())
            {
                using (var clientThrottle = new SemaphoreSlim(this.MaxClients, this.MaxClients))
                {
                    Log.LogMessage($"Uploading {taskItems.Count()} items...");
                    await Task.WhenAll(taskItems.Select(
                        item => blobFeedAction.UploadAssetAsync(
                            item,
                            clientThrottle,
                            UploadTimeoutInMinutes,
                            CreatePushOptions())));
                }
            }
        }

        private static IEnumerable<PackageArtifactModel> ConcatPackageArtifacts(
            IEnumerable<PackageArtifactModel> artifacts,
            IEnumerable<ITaskItem> items)
        {
            return artifacts.Concat(items
                .Select(CreatePackageArtifactModel));
        }

        private static IEnumerable<BlobArtifactModel> ConcatBlobArtifacts(
            IEnumerable<BlobArtifactModel> artifacts,
            IEnumerable<ITaskItem> items)
        {
            return artifacts.Concat(items
                .Select(CreateBlobArtifactModel)
                .Where(blob => blob != null));
        }

        private static PackageArtifactModel CreatePackageArtifactModel(ITaskItem item)
        {
            NupkgInfo info = new NupkgInfo(item.ItemSpec);

            return new PackageArtifactModel
            {
                Attributes = ParseCustomAttributes(item),
                Id = info.Id,
                Version = info.Version
            };
        }

        private static BlobArtifactModel CreateBlobArtifactModel(ITaskItem item)
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

        private PushOptions CreatePushOptions()
        {
            return new PushOptions
            {
                AllowOverwrite = Overwrite,
                PassIfExistingItemIdentical = PassIfExistingItemIdentical
            };
        }
    }
}