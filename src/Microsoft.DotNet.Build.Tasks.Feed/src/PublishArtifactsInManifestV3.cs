// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
using Microsoft.DotNet.Maestro.Client;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class PublishArtifactsInManifestV3 : PublishArtifactsInManifestBase
    {
        /// <summary>
        /// Comma separated list of Maestro++ Channel IDs to which the build should
        /// be assigned to once the assets are published.
        /// </summary>
        [Required]
        public string TargetChannels { get; set; }

        [Required] 
        public string AzureDevOpsFeedsKey { get; set; }

        [Required] 
        public string AzureStorageTargetFeedKey { get; set; }

        [Required] 
        public string InstallersFeedKey { get; set; }

        [Required] 
        public string CheckSumsFeedKey { get; set; }

        public string InternalInstallersFeedKey { get; set; }

        public string InternalCheckSumsFeedKey { get; set; }

        public bool PublishInstallersAndChecksums { get; set; }

        public string PdbArtifactsBasePath { get; set; }

        public string MsdlToken { get; set; }

        public string SymWebToken { get; set; }

        public string SymbolPublishingExclusionsFile { get; set; }

        public bool PublishSpecialClrFiles { get; set; }

        public bool AllowFeedOverrides { get; set; }

        public string InstallersFeedOverride { get; set; }

        public string ChecksumsFeedOverride { get; set; }

        public string ShippingFeedOverride { get; set; }

        public string TransportFeedOverride { get; set; }

        public string SymbolsFeedOverride { get; set; }

        public override bool Execute()
        {
            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }

        public override async Task<bool> ExecuteAsync()
        {
            if (AnyMissingRequiredProperty())
            {
                Log.LogError("Missing required properties. Aborting execution.");
                return false;
            }

            try
            {
                List<int> targetChannelsIds = new List<int>();

                foreach (var channelIdStr in TargetChannels.Split('-'))
                {
                    if (!int.TryParse(channelIdStr, out var channelId))
                    {
                        Log.LogError($"Value '{channelIdStr}' isn't recognized as a valid Maestro++ channel ID. To add a channel refer to https://github.com/dotnet/arcade/blob/master/Documentation/CorePackages/Publishing.md#how-to-add-a-new-channel-to-use-v3-publishing.");
                        continue;
                    }

                    targetChannelsIds.Add(channelId);
                }

                if (Log.HasLoggedErrors)
                {
                    Log.LogError($"Could not parse the target channels list '{TargetChannels}'. It should be a comma separated list of integers.");
                    return false;
                }

                string temporarySymbolsLocation =
                    Path.GetFullPath(Path.Combine(BlobAssetsBasePath, @"..\", "tempSymbols"));

                EnsureTemporarySymbolDirectoryExists(temporarySymbolsLocation);

                SplitArtifactsInCategories(BuildModel);
                DeleteSymbolTemporaryFiles(temporarySymbolsLocation);

                //Copying symbol files to temporary location is required because the symUploader API needs read/write access to the files,
                //since we publish blobs and symbols in parallel this will cause IO exceptions.
                CopySymbolFilesToTemporaryLocation(BuildModel, temporarySymbolsLocation);

                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                // Fetch Maestro record of the build. We're going to use it to get the BAR ID
                // of the assets being published so we can add a new location for them.
                IMaestroApi client = ApiFactory.GetAuthenticated(MaestroApiEndpoint, BuildAssetRegistryToken);
                Maestro.Client.Models.Build buildInformation = await client.Builds.GetBuildAsync(BARBuildId);
                Dictionary<string, HashSet<Asset>> buildAssets = CreateBuildAssetDictionary(buildInformation);

                foreach (var targetChannelId in targetChannelsIds)
                {
                    TargetChannelConfig targetChannelConfig = PublishingConstants.ChannelInfos
                        .Where(ci =>
                            ci.Id == targetChannelId &&
                            (ci.PublishingInfraVersion == PublishingInfraVersion.All ||
                             ci.PublishingInfraVersion == PublishingInfraVersion.Next))
                        .FirstOrDefault();

                    // Invalid channel ID was supplied
                    if (targetChannelConfig.Equals(default(TargetChannelConfig)))
                    {
                        Log.LogError($"Channel with ID '{targetChannelId}' is not configured to be published to.");
                        return false;
                    }

                    if (await client.Channels.GetChannelAsync(targetChannelId) == null)
                    {
                        Log.LogError($"Channel with ID '{targetChannelId}' does not exist in BAR.");
                        return false;
                    }

                    Log.LogMessage(MessageImportance.High, $"Publishing to this target channel: {targetChannelConfig}");

                    string shortLinkUrl = string.IsNullOrEmpty(targetChannelConfig.AkaMSChannelName) ?
                        $"dotnet/" : $"dotnet/{targetChannelConfig.AkaMSChannelName}/{BuildQuality}";

                    var targetFeedsSetup = new SetupTargetFeedConfigV3(
                        targetChannelConfig.IsInternal,
                        BuildModel.Identity.IsStable,
                        BuildModel.Identity.Name,
                        BuildModel.Identity.Commit,
                        AzureStorageTargetFeedKey,
                        PublishInstallersAndChecksums,
                        GetFeed(targetChannelConfig.InstallersFeed, InstallersFeedOverride),
                        targetChannelConfig.IsInternal? InternalInstallersFeedKey : InstallersFeedKey,
                        GetFeed(targetChannelConfig.ChecksumsFeed, ChecksumsFeedOverride),
                        targetChannelConfig.IsInternal? InternalCheckSumsFeedKey : CheckSumsFeedKey,
                        GetFeed(targetChannelConfig.ShippingFeed, ShippingFeedOverride),
                        GetFeed(targetChannelConfig.TransportFeed, TransportFeedOverride),
                        GetFeed(targetChannelConfig.SymbolsFeed, SymbolsFeedOverride),
                        shortLinkUrl,
                        AzureDevOpsFeedsKey,
                        BuildEngine = this.BuildEngine,
                        targetChannelConfig.SymbolTargetType,
                        filesToExclude: targetChannelConfig.FilenamesToExclude);

                    var targetFeedConfigs = targetFeedsSetup.Setup();

                    // No target feeds to publish to, very likely this is an error
                    if (targetFeedConfigs.Count() == 0)
                    {
                        Log.LogError($"No target feeds were found to publish the assets to.");
                        return false;
                    }

                    foreach (var feedConfig in targetFeedConfigs)
                    {
                        Log.LogMessage(MessageImportance.High, $"Target feed config: {feedConfig}");

                        TargetFeedContentType categoryKey = feedConfig.ContentType;
                        if (!FeedConfigs.TryGetValue(categoryKey, out _))
                        {
                            FeedConfigs[categoryKey] = new HashSet<TargetFeedConfig>();
                        }

                        FeedConfigs[categoryKey].Add(feedConfig);
                    }
                }

                CheckForStableAssetsInNonIsolatedFeeds();

                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                await Task.WhenAll(new Task[] {
                    HandlePackagePublishingAsync(buildAssets),
                    HandleBlobPublishingAsync(buildAssets),
                    HandleSymbolPublishingAsync(PdbArtifactsBasePath, MsdlToken,
                        SymWebToken, SymbolPublishingExclusionsFile, temporarySymbolsLocation, PublishSpecialClrFiles)
                });

                DeleteSymbolTemporaryFiles(temporarySymbolsLocation);
                DeleteSymbolTemporaryDirectory(temporarySymbolsLocation);
                Log.LogMessage(MessageImportance.High, "Successfully deleted the temporary symbols directory.");
                await PersistPendingAssetLocationAsync(client);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            if (!Log.HasLoggedErrors)
            {
                Log.LogMessage(MessageImportance.High, "Publishing finished with success.");
            }

            return !Log.HasLoggedErrors;
        }


        /// <summary>
        /// Copying symbol files to temporary location.
        /// </summary>
        /// <param name="buildModel"></param>
        /// <param name="symbolTemporaryLocation"></param>
        private void CopySymbolFilesToTemporaryLocation(BuildModel buildModel, string symbolTemporaryLocation)
        {
            foreach (var blobAsset in buildModel.Artifacts.Blobs)
            {
                if (blobAsset.Id.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
                {
                    var sourceFile = Path.Combine(BlobAssetsBasePath, Path.GetFileName(blobAsset.Id));
                    var destinationFile = Path.Combine(symbolTemporaryLocation, Path.GetFileName(blobAsset.Id));
                    File.Copy(sourceFile, destinationFile);
                    Log.LogMessage(MessageImportance.Low,
                        $"Successfully copied file {sourceFile} to {destinationFile}.");
                }
            }
        }

        /// <summary>
        /// Create Temporary Symbols directory if it does not exists.
        /// </summary>
        /// <param name="temporarySymbolsLocation"></param>
        public void EnsureTemporarySymbolDirectoryExists(string temporarySymbolsLocation)
        {
            if (!Directory.Exists(temporarySymbolsLocation))
            {
                Directory.CreateDirectory(temporarySymbolsLocation);
            }
        }
        /// <summary>
        /// Delete the symbols files after publishing to Symbol server(s), this is part of cleanup
        /// </summary>
        /// <param name="temporarySymbolsLocation"></param>
        public void DeleteSymbolTemporaryFiles(string temporarySymbolsLocation)
        {
            try
            {
                if (Directory.Exists(temporarySymbolsLocation))
                {
                    string[] fileEntries = Directory.GetFiles(temporarySymbolsLocation);
                    foreach (var file in fileEntries)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning(ex.Message);
            }
        }

        /// <summary>
        /// Deletes the temporary symbol folder, this is part of clean up
        /// </summary>
        /// <param name="temporarySymbolLocation"></param>
        public void DeleteSymbolTemporaryDirectory(string temporarySymbolLocation)
        {
            if (Directory.Exists(temporarySymbolLocation))
            {
                Directory.Delete(temporarySymbolLocation);
            }
        }

        public string GetFeed(string feed, string feedOverride)
        {
            return (AllowFeedOverrides && !string.IsNullOrEmpty(feedOverride)) ? feedOverride : feed;
        }
    }
}
