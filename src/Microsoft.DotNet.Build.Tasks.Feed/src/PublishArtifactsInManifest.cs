// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.CloudTestTasks;
using Microsoft.DotNet.Maestro.Client;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    /// <summary>
    /// The intended use of this task is to push artifacts described in
    /// a build manifest to a static package feed.
    /// </summary>
    public class PublishArtifactsInManifest : MSBuild.Task
    {
        // Matches package feeds like
        // https://dotnet-feed-internal.azurewebsites.net/container/dotnet-core-internal/sig/dsdfasdfasdf234234s/se/2020-02-02/darc-int-dotnet-arcade-services-babababababe-08/index.json
        const string AzureStorageProxyFeedPattern =
            @"(?<feedURL>https://([a-z-]+).azurewebsites.net/container/(?<container>[^/]+)/sig/\w+/se/([0-9]{4}-[0-9]{2}-[0-9]{2})/(?<baseFeedName>darc-(?<type>int|pub)-(?<repository>.+?)-(?<sha>[A-Fa-f0-9]{7,40})-?(?<subversion>\d*)/))index.json";

        // Matches package feeds like the one below. Special case for static internal proxy-backed feed
        // https://dotnet-feed-internal.azurewebsites.net/container/dotnet-core-internal/sig/dsdfasdfasdf234234s/se/2020-02-02/darc-int-dotnet-arcade-services-babababababe-08/index.json
        const string AzureStorageProxyFeedStaticPattern =
            @"(?<feedURL>https://([a-z-]+).azurewebsites.net/container/(?<container>[^/]+)/sig/\w+/se/([0-9]{4}-[0-9]{2}-[0-9]{2})/(?<baseFeedName>[^/]+/))index.json";

        // Matches package feeds like
        // https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json
        const string AzureStorageStaticBlobFeedPattern =
            @"https://([a-z-]+).blob.core.windows.net/[^/]+/index.json";

        // Matches package feeds like
        // https://pkgs.dev.azure.com/dnceng/public/_packaging/public-feed-name/nuget/v3/index.json
        // or https://pkgs.dev.azure.com/dnceng/_packaging/internal-feed-name/nuget/v3/index.json
        public const string AzDoNuGetFeedPattern = 
            @"https://pkgs.dev.azure.com/(?<account>[a-zA-Z0-9]+)/(?<visibility>[a-zA-Z0-9-]+/)?_packaging/(?<feed>.+)/nuget/v3/index.json";

        /// <summary>
        /// Configuration telling which target feed to use for each artifact category.
        /// ItemSpec: ArtifactCategory
        /// Metadata TargetURL: target URL where assets of this category should be published to.
        /// Metadata Type: type of the target feed.
        /// Metadata Token: token to be used for publishing to target feed.
        /// </summary>
        [Required]
        public ITaskItem[] TargetFeedConfig { get; set; }

        /// <summary>
        /// Full path to the assets to publish manifest.
        /// </summary>
        [Required]
        public string AssetManifestPath { get; set; }

        /// <summary>
        /// Full path to the folder containing blob assets.
        /// </summary>
        [Required]
        public string BlobAssetsBasePath { get; set; }

        /// <summary>
        /// Full path to the folder containing package assets.
        /// </summary>
        [Required]
        public string PackageAssetsBasePath { get; set; }

        /// <summary>
        /// ID of the build (in BAR/Maestro) that produced the artifacts being published.
        /// This might change in the future as we'll probably fetch this ID from the manifest itself.
        /// </summary>
        [Required]
        public int BARBuildId { get; set; }

        /// <summary>
        /// Access point to the Maestro API to be used for accessing BAR.
        /// </summary>
        [Required]
        public string MaestroApiEndpoint { get; set; }

        /// <summary>
        /// Authentication token to be used when interacting with Maestro API.
        /// </summary>
        [Required]
        public string BuildAssetRegistryToken { get; set; }

        /// <summary>
        /// Maximum number of parallel uploads for the upload tasks
        /// </summary>
        public int MaxClients { get; set; } = 8;

        /// <summary>
        /// Directory where "nuget.exe" is installed. This will be used to publish packages.
        /// </summary>
        [Required]
        public string NugetPath { get; set; }

        public readonly Dictionary<string, List<FeedConfig>> FeedConfigs = new Dictionary<string, List<FeedConfig>>();

        private readonly Dictionary<string, List<PackageArtifactModel>> PackagesByCategory = new Dictionary<string, List<PackageArtifactModel>>();

        private readonly Dictionary<string, List<BlobArtifactModel>> BlobsByCategory = new Dictionary<string, List<BlobArtifactModel>>();


        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Publishing artifacts to feed.");

                if (string.IsNullOrWhiteSpace(AssetManifestPath) || !File.Exists(AssetManifestPath))
                {
                    Log.LogError($"Problem reading asset manifest path from '{AssetManifestPath}'");
                }

                if (!Directory.Exists(BlobAssetsBasePath))
                {
                    Log.LogError($"Problem reading blob assets from {BlobAssetsBasePath}");
                }

                if (!Directory.Exists(PackageAssetsBasePath))
                {
                    Log.LogError($"Problem reading package assets from {PackageAssetsBasePath}");
                }

                var buildModel = BuildManifestUtil.ManifestFileToModel(AssetManifestPath, Log);

                // Parsing the manifest may fail for several reasons
                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                // Fetch Maestro record of the build. We're going to use it to get the BAR ID
                // of the assets being published so we can add a new location for them.
                IMaestroApi client = ApiFactory.GetAuthenticated(MaestroApiEndpoint, BuildAssetRegistryToken);
                Maestro.Client.Models.Build buildInformation = await client.Builds.GetBuildAsync(BARBuildId);

                ParseTargetFeedConfig();

                // Return errors from parsing FeedConfig
                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                SplitArtifactsInCategories(buildModel);

                await HandlePackagePublishingAsync(client, buildInformation);

                await HandleBlobPublishingAsync(client, buildInformation);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        ///     Parse out the input TargetFeedConfig into a dictionary of FeedConfig types
        /// </summary>
        public void ParseTargetFeedConfig()
        {
            foreach (var fc in TargetFeedConfig)
            {
                string targetFeedUrl = fc.GetMetadata("TargetURL");
                string feedKey = fc.GetMetadata("Token");
                string type = fc.GetMetadata("Type");

                if (string.IsNullOrEmpty(targetFeedUrl) ||
                    string.IsNullOrEmpty(feedKey) ||
                    string.IsNullOrEmpty(type))
                {
                    Log.LogError($"Invalid FeedConfig entry. TargetURL='{targetFeedUrl}' Type='{type}' Token='{feedKey}'");
                    continue;
                }

                if (!Enum.TryParse<FeedType>(type, true, out FeedType feedType))
                {
                    Log.LogError($"Invalid feed config type '{type}'. Possible values are: {string.Join(", ", Enum.GetNames(typeof(FeedType)))}");
                    continue;
                }

                var feedConfig = new FeedConfig()
                {
                    TargetFeedURL = targetFeedUrl,
                    Type = feedType,
                    FeedKey = feedKey
                };

                string assetSelection = fc.GetMetadata("AssetSelection");
                if (!string.IsNullOrEmpty(assetSelection))
                {
                    if (!Enum.TryParse<AssetSelection>(assetSelection, true, out AssetSelection selection))
                    {
                        Log.LogError($"Invalid feed config asset selection '{type}'. Possible values are: {string.Join(", ", Enum.GetNames(typeof(AssetSelection)))}");
                        continue;
                    }
                    feedConfig.AssetSelection = selection;
                }

                string categoryKey = fc.ItemSpec.Trim().ToUpper();
                if (!FeedConfigs.TryGetValue(categoryKey, out var feedsList))
                {
                    FeedConfigs[categoryKey] = new List<FeedConfig>();
                }
                FeedConfigs[categoryKey].Add(feedConfig);
            }
        }

        private async Task HandlePackagePublishingAsync(IMaestroApi client, Maestro.Client.Models.Build buildInformation)
        {
            foreach (var packagesPerCategory in PackagesByCategory)
            {
                var category = packagesPerCategory.Key;
                var packages = packagesPerCategory.Value;

                if (FeedConfigs.TryGetValue(category, out List<FeedConfig> feedConfigsForCategory))
                {
                    foreach (var feedConfig in feedConfigsForCategory)
                    {
                        List<PackageArtifactModel> filteredPackages = FilterPackages(packages, feedConfig);

                        switch (feedConfig.Type)
                        {
                            case FeedType.AzDoNugetFeed:
                                await PublishPackagesToAzDoNugetFeedAsync(filteredPackages, client, buildInformation, feedConfig);
                                break;
                            case FeedType.AzureStorageFeed:
                                await PublishPackagesToAzureStorageNugetFeedAsync(filteredPackages, client, buildInformation, feedConfig);
                                break;
                            default:
                                Log.LogError($"Unknown target feed type for category '{category}': '{feedConfig.Type}'.");
                                break;
                        }
                    }
                }
                else
                {
                    Log.LogError($"No target feed configuration found for artifact category: '{category}'.");
                }
            }
        }

        private List<PackageArtifactModel> FilterPackages(List<PackageArtifactModel> packages, FeedConfig feedConfig)
        {
            switch (feedConfig.AssetSelection)
            {
                case AssetSelection.All:
                    // No filtering needed
                    return packages;
                case AssetSelection.NonShippingOnly:
                    return packages.Where(p => p.NonShipping).ToList();
                case AssetSelection.ShippingOnly:
                    return packages.Where(p => !p.NonShipping).ToList();
                default:
                    // Throw NYI here instead of logging an error because error would have already been logged in the
                    // parser for the user.
                    throw new NotImplementedException("Unknown asset selection type '{feedConfig.AssetSelection}'");
            }
        }

        private async Task HandleBlobPublishingAsync(IMaestroApi client, Maestro.Client.Models.Build buildInformation)
        {
            foreach (var blobsPerCategory in BlobsByCategory)
            {
                var category = blobsPerCategory.Key;
                var blobs = blobsPerCategory.Value;

                if (FeedConfigs.TryGetValue(category, out List<FeedConfig> feedConfigsForCategory))
                {
                    foreach (var feedConfig in feedConfigsForCategory)
                    {
                        List<BlobArtifactModel> filteredBlobs = FilterBlobs(blobs, feedConfig);

                        switch (feedConfig.Type)
                        {
                            case FeedType.AzDoNugetFeed:
                                await PublishBlobsToAzDoNugetFeedAsync(filteredBlobs, client, buildInformation, feedConfig);
                                break;
                            case FeedType.AzureStorageFeed:
                                await PublishBlobsToAzureStorageNugetFeedAsync(filteredBlobs, client, buildInformation, feedConfig);
                                break;
                            default:
                                Log.LogError($"Unknown target feed type for category '{category}': '{feedConfig.Type}'.");
                                break;
                        }
                    }
                }
                else
                {
                    Log.LogError($"No target feed configuration found for artifact category: '{category}'.");
                }
            }
        }

        /// <summary>
        ///     Filter the blobs by the feed config information
        /// </summary>
        /// <param name="blobs"></param>
        /// <param name="feedConfig"></param>
        /// <returns></returns>
        private List<BlobArtifactModel> FilterBlobs(List<BlobArtifactModel> blobs, FeedConfig feedConfig)
        {
            // If the feed config wants further filtering, do that now.
            List<BlobArtifactModel> filteredBlobs = null;
            switch (feedConfig.AssetSelection)
            {
                case AssetSelection.All:
                    // No filtering needed
                    filteredBlobs = blobs;
                    break;
                case AssetSelection.NonShippingOnly:
                    filteredBlobs = blobs.Where(p => p.NonShipping).ToList();
                    break;
                case AssetSelection.ShippingOnly:
                    filteredBlobs = blobs.Where(p => !p.NonShipping).ToList();
                    break;
                default:
                    // Throw NYI here instead of logging an error because error would have already been logged in the
                    // parser for the user.
                    throw new NotImplementedException("Unknown asset selection type '{feedConfig.AssetSelection}'");
            }

            return filteredBlobs;
        }

        /// <summary>
        ///     Split the artifacts into categories.
        ///     
        ///     Categories are either specified explicitly when publishing (with the asset attribute "Category", separated by ';'),
        ///     or they are inferred based on the extension of the asset.
        /// </summary>
        /// <param name="buildModel"></param>
        private void SplitArtifactsInCategories(BuildModel buildModel)
        {
            foreach (var packageAsset in buildModel.Artifacts.Packages)
            {
                string categories = string.Empty;

                if (!packageAsset.Attributes.TryGetValue("Category", out categories))
                {
                    categories = InferCategory(packageAsset.Id);
                }

                foreach (var category in categories.Split(';').Select(c => c.ToUpper()))
                {
                    if (PackagesByCategory.ContainsKey(category))
                    {
                        PackagesByCategory[category].Add(packageAsset);
                    }
                    else
                    {
                        PackagesByCategory[category] = new List<PackageArtifactModel>() { packageAsset };
                    }
                }
            }

            foreach (var blobAsset in buildModel.Artifacts.Blobs)
            {
                string categories = string.Empty;

                if (!blobAsset.Attributes.TryGetValue("Category", out categories))
                {
                    categories = InferCategory(blobAsset.Id);
                }

                foreach (var category in categories.Split(';'))
                {
                    if (BlobsByCategory.ContainsKey(category))
                    {
                        BlobsByCategory[category].Add(blobAsset);
                    }
                    else
                    {
                        BlobsByCategory[category] = new List<BlobArtifactModel>() { blobAsset };
                    }
                }
            }
        }

        private async Task PublishPackagesToAzDoNugetFeedAsync(
            List<PackageArtifactModel> packagesToPublish,
            IMaestroApi client,
            Maestro.Client.Models.Build buildInformation,
            FeedConfig feedConfig)
        {
            foreach (var package in packagesToPublish)
            {
                var assetRecord = buildInformation.Assets
                    .Where(a => a.Name.Equals(package.Id) && a.Version.Equals(package.Version))
                    .FirstOrDefault();

                if (assetRecord == null)
                {
                    Log.LogError($"Asset with Id {package.Id}, Version {package.Version} isn't registered on the BAR Build with ID {BARBuildId}");
                    continue;
                }

                var assetWithLocations = await client.Assets.GetAssetAsync(assetRecord.Id);

                if (assetWithLocations?.Locations.Any(al => al.Location.Equals(feedConfig.TargetFeedURL, StringComparison.OrdinalIgnoreCase)) ?? false)
                {
                    Log.LogMessage($"Asset with Id {package.Id}, Version {package.Version} already has location {feedConfig.TargetFeedURL}");
                    continue;
                }

                await client.Assets.AddAssetLocationToAssetAsync(assetRecord.Id, AddAssetLocationToAssetAssetLocationType.NugetFeed, feedConfig.TargetFeedURL);
            }

            await PushNugetPackagesAsync(packagesToPublish, feedConfig, maxClients: MaxClients);
        }

        /// <summary>
        ///     Start a process as an async Task.
        /// </summary>
        /// <param name="path">Path to process</param>
        /// <param name="arguments">Process arguments</param>
        /// <returns>Process return code</returns>
        public Task<int> StartProcessAsync(string path, string arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo(path, arguments)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
            };

            Process process = new Process
            {
                StartInfo = info,
                EnableRaisingEvents = true
            };

            var completionSource = new TaskCompletionSource<int>();

            process.Exited += (obj, args) =>
            {
                completionSource.SetResult(((Process)obj).ExitCode);
                process.Dispose();
            };

            process.Start();

            return completionSource.Task;
        }

        /// <summary>
        ///     Push nuget packages to the azure devops feed.
        /// </summary>
        /// <param name="packagesToPublish">List of packages to publish</param>
        /// <param name="feedConfig">Information about feed to publish ot</param>
        /// <returns>Async task.</returns>
        public async Task PushNugetPackagesAsync(List<PackageArtifactModel> packagesToPublish, FeedConfig feedConfig, int maxClients)
        {
            var parsedUri = Regex.Match(feedConfig.TargetFeedURL, PublishArtifactsInManifest.AzDoNuGetFeedPattern);
            if (!parsedUri.Success)
            {
                Log.LogError($"Azure DevOps NuGetFeed was not in the expected format '{PublishArtifactsInManifest.AzDoNuGetFeedPattern}'");
                return;
            }
            string feedAccount = parsedUri.Groups["account"].Value;
            string feedVisibility = parsedUri.Groups["visibility"].Value;
            string feedName = parsedUri.Groups["feed"].Value;

            using (var clientThrottle = new SemaphoreSlim(maxClients, maxClients))
            {
                using (HttpClient httpClient = new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true }))
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                        "Basic",
                        Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", feedConfig.FeedKey))));

                    Log.LogMessage(MessageImportance.High, $"Pushing {packagesToPublish.Count()} packages.");
                    await System.Threading.Tasks.Task.WhenAll(packagesToPublish.Select(async packageToPublish =>
                    {
                        try
                        {
                            // Wait to avoid starting too many processes.
                            await clientThrottle.WaitAsync();
                            await PushNugetPackageAsync(feedConfig, httpClient, packageToPublish, feedAccount, feedVisibility, feedName);
                        }
                        finally
                        {
                            clientThrottle.Release();
                        }
                    }));
                }
            }
        }

        /// <summary>
        ///     Push a single package to the azure devops nuget feed.
        /// </summary>
        /// <param name="feedConfig">Feed</param>
        /// <param name="packageToPublish">Package to push</param>
        /// <returns>Task</returns>
        /// <remarks>
        ///     This method attempts to take the most efficient path to push the package.
        ///     There are two cases:
        ///         - The package does not exist, and is pushed normally
        ///         - The package exists, and its contents may or may not be equivalent.
        ///     The second case is is by far the most common. So, we first attempt to push the package normally using nuget.exe.
        ///     If this fails, this could mean any number of things (like failed auth). But in normal circumstances, this might
        ///     mean the package already exists. This either means that we are attempting to push the same package, or attemtping to push
        ///     a different package with the same id and version. The second case is an error, as azure devops feeds are immutable, the former
        ///     is simply a case where we should continue onward.
        /// </remarks>
        private async Task PushNugetPackageAsync(FeedConfig feedConfig, HttpClient client, PackageArtifactModel packageToPublish,
            string feedAccount, string feedVisibility, string feedName)
        {
            Log.LogMessage(MessageImportance.High, $"Pushing package '{packageToPublish.Id}' to feed {feedConfig.TargetFeedURL}");

            PackageAssetsBasePath = PackageAssetsBasePath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            string localPackageLocation = $"{PackageAssetsBasePath}{packageToPublish.Id}.{packageToPublish.Version}.nupkg";
            if (!File.Exists(localPackageLocation))
            {
                Log.LogError($"Could not locate '{packageToPublish.Id}.{packageToPublish.Version}' at '{localPackageLocation}'");
                return;
            }

            try
            {
                // The feed key when pushing to AzDo feeds is "AzureDevOps" (works with the credential helper).
                int result = await StartProcessAsync(NugetPath, $"push \"{localPackageLocation}\" -Source \"{feedConfig.TargetFeedURL}\" -NonInteractive -ApiKey AzureDevOps");
                if (result != 0)
                {
                    Log.LogMessage(MessageImportance.Low, $"Failed to push {localPackageLocation}, attempting to determine whether the package already exists on the feed with the same content.");

                    try
                    {
                        string packageContentUrl = $"https://pkgs.dev.azure.com/{feedAccount}/{feedVisibility}_apis/packaging/feeds/{feedName}/nuget/packages/{packageToPublish.Id}/versions/{packageToPublish.Version}/content";

                        if (await IsLocalPackageIdenticalToFeedPackage(localPackageLocation, packageContentUrl, client))
                        {
                            Log.LogMessage(MessageImportance.Normal, $"Package '{packageToPublish.Id}@{packageToPublish.Version}' already exists on '{feedConfig.TargetFeedURL}' but has the same content. Skipping.");
                        }
                        else
                        {
                            Log.LogError($"Package '{packageToPublish.Id}@{packageToPublish.Version}' already exists on '{feedConfig.TargetFeedURL}' with different content.");
                        }

                        return;
                    }
                    catch (Exception e)
                    {
                        // This is an error. It means we were unable to push using nuget, and then could not access to the package otherwise.
                        Log.LogWarning($"Failed to determine whether an existing package on the feed has the same content as '{localPackageLocation}': {e.Message}");
                    }

                    Log.LogError($"Failed to push '{packageToPublish.Id}@{packageToPublish.Version}'. Result code '{result}'.");
                }
            }
            catch (Exception e)
            {
                Log.LogError($"Unexpected exception pushing package '{packageToPublish.Id}@{packageToPublish.Version}': {e.Message}");
            }
        }

        /// <summary>
        ///     Determine whether a local package is the same as a package on an AzDO feed.
        /// </summary>
        /// <param name="localPackageFullPath"></param>
        /// <param name="packageContentUrl"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        /// <remarks>
        ///     Open a stream to the local file and an http request to the package. There are a couple possibilities:
        ///     - The returned headers includes a content MD5 header, in which case we can
        ///       hash the local file and just compare those.
        ///     - No content MD5 hash, and the streams must be compared in blocks. This is a bit trickier to do efficiently,
        ///       since we do not necessarily want to read all bytes if we can help it. Thus, we should compare in blocks.  However,
        ///       the streams make no gaurantee that they will return a full block each time when read operations are performed, so we
        ///       must be sure to only compare the minimum number of bytes returned.
        /// </remarks>
        private async Task<bool> IsLocalPackageIdenticalToFeedPackage(string localPackageFullPath, string packageContentUrl, HttpClient client)
        {
            Log.LogMessage($"Getting package content from {packageContentUrl} and comparing to {localPackageFullPath}");

            try
            {
                using (Stream localFileStream = File.OpenRead(localPackageFullPath))
                using (HttpResponseMessage response = await client.GetAsync(packageContentUrl))
                {
                    response.EnsureSuccessStatusCode();

                    // Check the headers for content length and md5
                    bool md5HeaderAvailable = response.Headers.TryGetValues("Content-MD5", out var md5);
                    bool lengthHeaderAvailable = response.Headers.TryGetValues("Content-Length", out var contentLength);

                    if (lengthHeaderAvailable && long.Parse(contentLength.Single()) != localFileStream.Length)
                    {
                        Log.LogMessage(MessageImportance.Low, $"Package '{localPackageFullPath}' has different length than remote package '{packageContentUrl}'.");
                        return false;
                    }

                    if (md5HeaderAvailable)
                    {
                        var localMD5 = AzureStorageUtils.CalculateMD5(localPackageFullPath);
                        if (!localMD5.Equals(md5.Single(), StringComparison.OrdinalIgnoreCase))
                        {
                            Log.LogMessage(MessageImportance.Low, $"Package '{localPackageFullPath}' has different MD5 hash than remote package '{packageContentUrl}'.");
                        }

                        return true;
                    }

                    const int BufferSize = 64 * 1024;

                    // Otherwise, compare the streams
                    var remoteStream = await response.Content.ReadAsStreamAsync();
                    return await CompareStreamsAsync(localFileStream, remoteStream, BufferSize);
                }
            }
            catch (Exception e)
            {
                // This is an error. It means we were unable to push using nuget, and then could not access to the package otherwise.
                Log.LogWarning($"Failed to determine whether an existing package on the feed has the same content: {e.Message}");
                return false;
            }
        }

        /// <summary>
        ///     Compare a local stream and a remote stream for quality
        /// </summary>
        /// <param name="localFileStream">Local stream</param>
        /// <param name="remoteStream">Remote stream</param>
        /// <param name="bufferSize">Buffer to keep around</param>
        /// <returns>True if the streams are equal, false otherwise.</returns>
        public static async Task<bool> CompareStreamsAsync(Stream localFileStream, Stream remoteStream, int bufferSize)
        {
            byte[] localBuffer = new byte[bufferSize];
            byte[] remoteBuffer = new byte[bufferSize];
            int localBufferWriteOffset = 0;
            int remoteBufferWriteOffset = 0;
            int localBufferReadOffset = 0;
            int remoteBufferReadOffset = 0;

            do
            {
                int localBytesToRead = bufferSize - localBufferWriteOffset;
                int remoteBytesToRead = bufferSize - remoteBufferWriteOffset;

                int bytesRemoteFile = 0;
                int bytesLocalFile = 0;
                if (remoteBytesToRead > 0)
                {
                    bytesRemoteFile = await remoteStream.ReadAsync(remoteBuffer, remoteBufferWriteOffset, remoteBytesToRead);
                }

                if (localBytesToRead > 0)
                {
                    bytesLocalFile = await localFileStream.ReadAsync(localBuffer, localBufferWriteOffset, localBytesToRead);
                }

                int bytesLocalAvailable = bytesLocalFile + (localBufferWriteOffset - localBufferReadOffset);
                int bytesRemoteAvailable = bytesRemoteFile + (remoteBufferWriteOffset - remoteBufferReadOffset);
                int minBytesAvailable = Math.Min(bytesLocalAvailable, bytesRemoteAvailable);

                if (minBytesAvailable == 0)
                {
                    // If there is nothing left to compare (EOS), then good to go.
                    // Otherwise, one stream reached EOS before the other.
                    return bytesLocalFile == bytesRemoteFile;
                }

                // Compare the minimum number of bytes between the two streams, starting at the offset,
                // then advance the offsets for the next pass
                for (int i = 0; i < minBytesAvailable; i++)
                {
                    if (remoteBuffer[remoteBufferReadOffset + i] != localBuffer[localBufferReadOffset + i])
                    {
                        return false;
                    }
                }

                // Advance the offsets. The read offset gets advanced by the amount that we actually compared,
                // While the write offset gets advanced by the amount each of the streams returned.
                localBufferReadOffset += minBytesAvailable;
                remoteBufferReadOffset += minBytesAvailable;

                localBufferWriteOffset += bytesLocalFile;
                remoteBufferWriteOffset += bytesRemoteFile;

                if (localBufferReadOffset == bufferSize)
                {
                    localBufferReadOffset = 0;
                    localBufferWriteOffset = 0;
                }

                if (remoteBufferReadOffset == bufferSize)
                {
                    remoteBufferReadOffset = 0;
                    remoteBufferWriteOffset = 0;
                }
            }
            while (true);
        }

        private async Task PublishBlobsToAzDoNugetFeedAsync(
            List<BlobArtifactModel> blobsToPublish,
            IMaestroApi client,
            Maestro.Client.Models.Build buildInformation,
            FeedConfig feedConfig)
        {
            foreach (var blob in blobsToPublish)
            {
                var assetRecord = buildInformation.Assets
                    .Where(a => a.Name.Equals(blob.Id))
                    .FirstOrDefault();

                if (assetRecord == null)
                {
                    Log.LogError($"Asset with Id {blob.Id} isn't registered on the BAR Build with ID {BARBuildId}");
                    continue;
                }

                var assetWithLocations = await client.Assets.GetAssetAsync(assetRecord.Id);

                if (assetWithLocations?.Locations.Any(al => al.Location.Equals(feedConfig.TargetFeedURL, StringComparison.OrdinalIgnoreCase)) ?? false)
                {
                    Log.LogMessage($"Asset with Id {blob.Id} already has location {feedConfig.TargetFeedURL}");
                    continue;
                }

                await client.Assets.AddAssetLocationToAssetAsync(assetRecord.Id, AddAssetLocationToAssetAssetLocationType.Container, feedConfig.TargetFeedURL);
            }
        }

        private async Task PublishPackagesToAzureStorageNugetFeedAsync(
            List<PackageArtifactModel> packagesToPublish,
            IMaestroApi client,
            Maestro.Client.Models.Build buildInformation,
            FeedConfig feedConfig)
        {
            PackageAssetsBasePath = PackageAssetsBasePath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) 
                + Path.DirectorySeparatorChar;

            var packages = packagesToPublish.Select(p => $"{PackageAssetsBasePath}{p.Id}.{p.Version}.nupkg");
            var blobFeedAction = CreateBlobFeedAction(feedConfig);

            var pushOptions = new PushOptions
            {
                AllowOverwrite = false,
                PassIfExistingItemIdentical = true
            };

            foreach (var package in packagesToPublish)
            {
                var assetRecord = buildInformation.Assets
                    .Where(a => a.Name.Equals(package.Id) && a.Version.Equals(package.Version))
                    .FirstOrDefault();

                if (assetRecord == null)
                {
                    Log.LogError($"Asset with Id {package.Id}, Version {package.Version} isn't registered on the BAR Build with ID {BARBuildId}");
                    continue;
                }

                var assetWithLocations = await client.Assets.GetAssetAsync(assetRecord.Id);

                if (assetWithLocations?.Locations.Any(al => al.Location.Equals(feedConfig.TargetFeedURL, StringComparison.OrdinalIgnoreCase)) ?? false)
                {
                    Log.LogMessage($"Asset with Id {package.Id}, Version {package.Version} already has location {feedConfig.TargetFeedURL}");
                    continue;
                }

                await client.Assets.AddAssetLocationToAssetAsync(assetRecord.Id, AddAssetLocationToAssetAssetLocationType.NugetFeed, feedConfig.TargetFeedURL);
            }

            await blobFeedAction.PushToFeedAsync(packages, pushOptions);
        }

        private async Task PublishBlobsToAzureStorageNugetFeedAsync(
            List<BlobArtifactModel> blobsToPublish,
            IMaestroApi client,
            Maestro.Client.Models.Build buildInformation,
            FeedConfig feedConfig)
        {
            BlobAssetsBasePath = BlobAssetsBasePath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) 
                + Path.DirectorySeparatorChar;

            var blobs = blobsToPublish
                .Select(blob =>
                {
                    var fileName = Path.GetFileName(blob.Id);
                    return new MSBuild.TaskItem($"{BlobAssetsBasePath}{fileName}", new Dictionary<string, string>
                    {
                        {"RelativeBlobPath", blob.Id}
                    });
                })
                .ToArray();

            var blobFeedAction = CreateBlobFeedAction(feedConfig);
            var pushOptions = new PushOptions
            {
                AllowOverwrite = false,
                PassIfExistingItemIdentical = true
            };

            foreach (var blob in blobsToPublish)
            {
                var assetRecord = buildInformation.Assets
                    .Where(a => a.Name.Equals(blob.Id))
                    .SingleOrDefault();

                if (assetRecord == null)
                {
                    Log.LogError($"Asset with Id {blob.Id} isn't registered on the BAR Build with ID {BARBuildId}");
                    continue;
                }

                var assetWithLocations = await client.Assets.GetAssetAsync(assetRecord.Id);

                if (assetWithLocations?.Locations.Any(al => al.Location.Equals(feedConfig.TargetFeedURL, StringComparison.OrdinalIgnoreCase)) ?? false)
                {
                    Log.LogMessage($"Asset with Id {blob.Id} already has location {feedConfig.TargetFeedURL}");
                    continue;
                }

                await client.Assets.AddAssetLocationToAssetAsync(assetRecord.Id, AddAssetLocationToAssetAssetLocationType.Container, feedConfig.TargetFeedURL);
            }

            await blobFeedAction.PublishToFlatContainerAsync(blobs, maxClients: MaxClients, pushOptions);
        }

        private BlobFeedAction CreateBlobFeedAction(FeedConfig feedConfig)
        {
            var proxyBackedFeedMatch = Regex.Match(feedConfig.TargetFeedURL, AzureStorageProxyFeedPattern);
            var proxyBackedStaticFeedMatch = Regex.Match(feedConfig.TargetFeedURL, AzureStorageProxyFeedStaticPattern);
            var azureStorageStaticBlobFeedMatch = Regex.Match(feedConfig.TargetFeedURL, AzureStorageStaticBlobFeedPattern);

            if (proxyBackedFeedMatch.Success || proxyBackedStaticFeedMatch.Success)
            {
                var regexMatch = (proxyBackedFeedMatch.Success) ? proxyBackedFeedMatch : proxyBackedStaticFeedMatch;
                var containerName = regexMatch.Groups["container"].Value;
                var baseFeedName = regexMatch.Groups["baseFeedName"].Value;
                var feedURL = regexMatch.Groups["feedURL"].Value;
                var storageAccountName = "dotnetfeed";

                // Initialize the feed using sleet
                SleetSource sleetSource = new SleetSource()
                {
                    Name = baseFeedName,
                    Type = "azure",
                    BaseUri = feedURL,
                    AccountName = storageAccountName,
                    Container = containerName,
                    FeedSubPath = baseFeedName,
                    ConnectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={feedConfig.FeedKey};EndpointSuffix=core.windows.net"
                };

                return new BlobFeedAction(sleetSource, feedConfig.FeedKey, Log);
            }
            else if (azureStorageStaticBlobFeedMatch.Success)
            {
                return new BlobFeedAction(feedConfig.TargetFeedURL, feedConfig.FeedKey, Log);
            }
            else
            {
                Log.LogError($"Could not parse Azure feed URL: '{feedConfig.TargetFeedURL}'");
                return null;
            }
        }

        /// <summary>
        ///     Infers the category based on the extension of the particular asset
        ///     
        ///     If no category can be inferred, then "NETCORE" is used.
        /// </summary>
        /// <param name="assetId">ID of asset</param>
        /// <returns>Asset cateogry</returns>
        private string InferCategory(string assetId)
        {
            var extension = Path.GetExtension(assetId).ToUpper();

            var whichCategory = new Dictionary<string, string>()
            {
                { ".NUPKG", "NETCORE" },
                { ".PKG", "OSX" },
                { ".DEB", "DEB" },
                { ".RPM", "RPM" },
                { ".NPM", "NODE" },
                { ".ZIP", "BINARYLAYOUT" },
                { ".MSI", "INSTALLER" },
                { ".SHA", "CHECKSUM" },
                { ".POM", "MAVEN" },
                { ".VSIX", "VSIX" },
            };

            if (whichCategory.TryGetValue(extension, out var category))
            {
                return category;
            }
            else
            {
                return "NETCORE";
            }
        }
    }

    public enum FeedType
    {
        AzDoNugetFeed,
        AzureStorageFeed
    }

    /// <summary>
    ///     Which assets from the category should be
    ///     added to the feed.
    /// </summary>
    public enum AssetSelection
    {
        All,
        ShippingOnly,
        NonShippingOnly
    }

    /// <summary>
    /// Hold properties of a target feed endpoint.
    /// </summary>
    public class FeedConfig
    {
        public string TargetFeedURL { get; set; }
        public FeedType Type { get; set; }
        public string FeedKey { get; set; }
        public AssetSelection AssetSelection { get; set; } = AssetSelection.All;
    }
}
