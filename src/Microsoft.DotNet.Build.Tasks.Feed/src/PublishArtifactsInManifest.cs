// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.CloudTestTasks;
using Microsoft.DotNet.Deployment.Tasks.Links.src;
using Microsoft.DotNet.Maestro.Client;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.DotNet.VersionTools.BuildManifest;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using Microsoft.DotNet.VersionTools.Util;
using NuGet.Packaging.Core;
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
        private const string ExpectedFeedUrlSuffix = "index.json";

        // Matches package feeds like
        // https://dotnet-feed-internal.azurewebsites.net/container/dotnet-core-internal/sig/dsdfasdfasdf234234s/se/2020-02-02/darc-int-dotnet-arcade-services-babababababe-08/index.json
        private const string AzureStorageProxyFeedPattern =
            @"(?<feedURL>https://([a-z-]+).azurewebsites.net/container/(?<container>[^/]+)/sig/\w+/se/([0-9]{4}-[0-9]{2}-[0-9]{2})/(?<baseFeedName>darc-(?<type>int|pub)-(?<repository>.+?)-(?<sha>[A-Fa-f0-9]{7,40})-?(?<subversion>\d*)/))index.json";

        // Matches package feeds like the one below. Special case for static internal proxy-backed feed
        // https://dotnet-feed-internal.azurewebsites.net/container/dotnet-core-internal/sig/dsdfasdfasdf234234s/se/2020-02-02/darc-int-dotnet-arcade-services-babababababe-08/index.json
        private const string AzureStorageProxyFeedStaticPattern =
            @"(?<feedURL>https://([a-z-]+).azurewebsites.net/container/(?<container>[^/]+)/sig/\w+/se/([0-9]{4}-[0-9]{2}-[0-9]{2})/(?<baseFeedName>[^/]+/))index.json";

        // Matches package feeds like
        // https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json
        private const string AzureStorageStaticBlobFeedPattern =
            @"https://([a-z-]+).blob.core.windows.net/[^/]+/index.json";

        // Matches package feeds like
        // https://pkgs.dev.azure.com/dnceng/public/_packaging/public-feed-name/nuget/v3/index.json
        // or https://pkgs.dev.azure.com/dnceng/_packaging/internal-feed-name/nuget/v3/index.json
        public const string AzDoNuGetFeedPattern =
            @"https://pkgs.dev.azure.com/(?<account>[a-zA-Z0-9]+)/(?<visibility>[a-zA-Z0-9-]+/)?_packaging/(?<feed>.+)/nuget/v3/index.json";
        private const string SymbolPackageSuffix = ".symbols.nupkg";
        private const string PackageSuffix = ".nupkg";
        private const string PackagesCategory = "PACKAGE";
        private const int MaxRetries = 5;

        /// <summary>
        /// Configuration telling which target feed to use for each artifact category.
        /// ItemSpec: ArtifactCategory
        /// Metadata TargetURL: target URL where assets of this category should be published to.
        /// Metadata Type: type of the target feed.
        /// Metadata Token: token to be used for publishing to target feed.
        /// Metadata AssetSelection (optional): Can be "All", "ShippingOnly" or "NonShippingOnly"
        ///                                     Determines which assets are pushed to this feed config
        /// Metadata Internal (optional): If true, the feed is only internally accessible.
        ///                               If false, the feed is publicly visible and internal builds wwill be rejected.
        ///                               If not provided, then this task will attempt to determine whether the feed URL is publicly visible or not.
        ///                               Unless SkipSafetyChecks is passed, the publishing infrastructure will check the accessibility of the feed.
        /// Metadata Isolated (optional): If true, stable packages can be pushed to this feed.
        ///                               If false, stable packages will be rejected.
        ///                               If not provided then defaults to false.
        /// Metadata AllowOverwrite (optional): If true, existing azure blob storage assets can be overwritten
        ///                                     If false, an error is thrown if an asset already exists
        ///                                     If not provided then defaults to false.
        ///                                     Azure DevOps feeds can never be overwritten.
        /// Metadata LatestLinkShortUrlPrefix (optional): If provided, AKA ms links are generated (for artifacts blobs only)
        ///                                               that target this short url path. The link is construct as such:
        ///                                               aka.ms/AkaShortUrlPath/BlobArtifactPath -> Target blob url
        ///                                               If specified, then AkaMSClientId, AkaMSClientSecret and AkaMSTenant must be provided.
        ///                                               The version information is stripped away from the file and blob artifact path.
        /// </summary>
        [Required]
        public ITaskItem[] TargetFeedConfig { get; set; }

        /// <summary>
        /// Full path to the assets to publish manifest(s)
        /// </summary>
        [Required]
        public ITaskItem[] AssetManifestPaths { get; set; }

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
        public int MaxClients { get; set; } = 16;

        /// <summary>
        /// Directory where "nuget.exe" is installed. This will be used to publish packages.
        /// </summary>
        [Required]
        public string NugetPath { get; set; }

        /// <summary>
        /// Whether this build is internal or not. If true, extra checks are done to avoid accidental
        /// publishing of assets to public feeds or storage accounts.
        /// </summary>
        [Required]
        public bool InternalBuild { get; set; }

        /// <summary>
        /// If true, safety checks only print messages and do not error
        /// - Internal asset to public feed
        /// - Stable packages to non-isolated feeds
        /// </summary>
        public bool SkipSafetyChecks { get; set; } = false;

        private ExponentialRetry RetryHandler = new ExponentialRetry
        {
            MaxAttempts = MaxRetries
        };

        #region Information for AKA MS link generation

        public string AkaMSClientId { get; set; }
        public string AkaMSClientSecret { get; set; }
        public string AkaMSTenant { get; set; }
        public string AkaMsOwners { get; set; }
        public string AkaMSCreatedBy { get; set; }
        public string AkaMSGroupOwner { get; set; }
        #endregion

        public readonly Dictionary<string, List<FeedConfig>> FeedConfigs = new Dictionary<string, List<FeedConfig>>();

        private readonly Dictionary<string, List<PackageArtifactModel>> PackagesByCategory = new Dictionary<string, List<PackageArtifactModel>>();

        private readonly Dictionary<string, List<BlobArtifactModel>> BlobsByCategory = new Dictionary<string, List<BlobArtifactModel>>();

        private AkaMSLinkManager _linkManager = null;

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                List<BuildModel> buildModels = new List<BuildModel>();
                foreach (var assetManifestPath in AssetManifestPaths)
                {
                    Log.LogMessage(MessageImportance.High, $"Publishing artifacts in {assetManifestPath.ItemSpec}.");
                    string fileName = assetManifestPath.ItemSpec;
                    if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
                    {
                        Log.LogError($"Problem reading asset manifest path from '{fileName}'");
                    }
                    else
                    {
                        buildModels.Add(BuildManifestUtil.ManifestFileToModel(assetManifestPath.ItemSpec, Log));
                    }
                }

                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                // Parsing the manifest may fail for several reasons
                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                // Fetch Maestro record of the build. We're going to use it to get the BAR ID
                // of the assets being published so we can add a new location for them.
                IMaestroApi client = ApiFactory.GetAuthenticated(MaestroApiEndpoint, BuildAssetRegistryToken);
                Maestro.Client.Models.Build buildInformation = await client.Builds.GetBuildAsync(BARBuildId);
                Dictionary<string, List<Asset>> buildAssets = CreateBuildAssetDictionary(buildInformation);

                await ParseTargetFeedConfigAsync();

                // Return errors from parsing FeedConfig
                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                foreach (var buildModel in buildModels)
                {
                    SplitArtifactsInCategories(buildModel);
                }

                // Return errors from the safety checks
                if (Log.HasLoggedErrors)
                {
                    return false;
                }

                await Task.WhenAll(new Task[] {
                        HandlePackagePublishingAsync(client, buildAssets),
                        HandleBlobPublishingAsync(client, buildAssets)
                    }
                );
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        ///     Lookup an asset in the build asset dictionary by name and version
        /// </summary>
        /// <param name="name">Name of asset</param>
        /// <param name="version">Version of asset</param>
        /// <returns>Asset if one with the name and version exists, null otherwise</returns>
        private Asset LookupAsset(string name, string version, Dictionary<string, List<Asset>> buildAssets)
        {
            if (!buildAssets.TryGetValue(name, out List<Asset> assetsWithName))
            {
                return null;
            }
            return assetsWithName.FirstOrDefault(asset => asset.Version == version);
        }

        /// <summary>
        ///     Lookup an asset in the build asset dictionary by name only.
        ///     This is for blob lookup purposes.
        /// </summary>
        /// <param name="name">Name of asset</param>
        /// <returns>
        ///     Asset if one with the name exists and is the only asset with the name.
        ///     Throws if there is more than one asset with that name.
        /// </returns>
        private Asset LookupAsset(string name, Dictionary<string, List<Asset>> buildAssets)
        {
            if (!buildAssets.TryGetValue(name, out List<Asset> assetsWithName))
            {
                return null;
            }
            return assetsWithName.Single();
        }

        /// <summary>
        ///     Build up a map of asset name -> asset list so that we can avoid n^2 lookups when processing assets.
        ///     We use name only because blobs are only looked up by id (version is not recorded in the manifest).
        ///     This could mean that there might be multiple versions of the same asset in the build.
        /// </summary>
        /// <param name="buildInformation">Build information</param>
        /// <returns>Map of asset name -> list of assets with that name.</returns>
        private Dictionary<string, List<Asset>> CreateBuildAssetDictionary(Maestro.Client.Models.Build buildInformation)
        {
            Dictionary<string, List<Asset>> buildAssets = new Dictionary<string, List<Asset>>();
            foreach (var asset in buildInformation.Assets)
            {
                if (buildAssets.TryGetValue(asset.Name, out List<Asset> assetsWithName))
                {
                    assetsWithName.Add(asset);
                }
                else
                {
                    buildAssets.Add(asset.Name, new List<Asset>() { asset });
                }
            }

            return buildAssets;
        }

        /// <summary>
        ///     Parse out the input TargetFeedConfig into a dictionary of FeedConfig types
        /// </summary>
        public async Task ParseTargetFeedConfigAsync()
        {
            using (HttpClient httpClient = new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true }))
            {
                foreach (var fc in TargetFeedConfig)
                {
                    string targetFeedUrl = fc.GetMetadata(nameof(FeedConfig.TargetURL));
                    string feedKey = fc.GetMetadata(nameof(FeedConfig.Token));
                    string type = fc.GetMetadata(nameof(FeedConfig.Type));

                    if (string.IsNullOrEmpty(targetFeedUrl) ||
                        string.IsNullOrEmpty(feedKey) ||
                        string.IsNullOrEmpty(type))
                    {
                        Log.LogError($"Invalid FeedConfig entry. {nameof(FeedConfig.TargetURL)}='{targetFeedUrl}' {nameof(FeedConfig.Type)}='{type}' {nameof(FeedConfig.Token)}='{feedKey}'");
                        continue;
                    }

                    if (!targetFeedUrl.EndsWith(ExpectedFeedUrlSuffix))
                    {
                        Log.LogError($"Exepcted that feed '{targetFeedUrl}' would end in {ExpectedFeedUrlSuffix}");
                        continue;
                    }

                    if (!Enum.TryParse<FeedType>(type, true, out FeedType feedType))
                    {
                        Log.LogError($"Invalid feed config type '{type}'. Possible values are: {string.Join(", ", Enum.GetNames(typeof(FeedType)))}");
                        continue;
                    }

                    var feedConfig = new FeedConfig()
                    {
                        TargetURL = targetFeedUrl,
                        Type = feedType,
                        Token = feedKey
                    };

                    string assetSelection = fc.GetMetadata(nameof(FeedConfig.AssetSelection));
                    if (!string.IsNullOrEmpty(assetSelection))
                    {
                        if (!Enum.TryParse<AssetSelection>(assetSelection, true, out AssetSelection selection))
                        {
                            Log.LogError($"Invalid feed config asset selection '{type}'. Possible values are: {string.Join(", ", Enum.GetNames(typeof(AssetSelection)))}");
                            continue;
                        }
                        feedConfig.AssetSelection = selection;
                    }

                    // To determine whether a feed is internal, we allow the user to
                    // specify the value explicitly.
                    string feedIsInternal = fc.GetMetadata(nameof(FeedConfig.Internal));
                    if (!string.IsNullOrEmpty(feedIsInternal))
                    {
                        if (!bool.TryParse(feedIsInternal, out bool feedSetting))
                        {
                            Log.LogError($"Invalid feed config '{nameof(FeedConfig.Internal)}' setting.  Must be 'true' or 'false'.");
                            continue;
                        }
                        feedConfig.Internal = feedSetting;
                    }
                    else
                    {
                        bool? isPublicFeed = await IsFeedPublicAsync(feedConfig.TargetURL, httpClient);
                        if (!isPublicFeed.HasValue)
                        {
                            continue;
                        }
                        else
                        {
                            feedConfig.Internal = !isPublicFeed.Value;
                        }
                    }

                    CheckForInternalBuildsOnPublicFeeds(feedConfig);

                    string feedIsIsolated = fc.GetMetadata(nameof(FeedConfig.Isolated));
                    if (!string.IsNullOrEmpty(feedIsIsolated))
                    {
                        if (!bool.TryParse(feedIsIsolated, out bool feedSetting))
                        {
                            Log.LogError($"Invalid feed config '{nameof(FeedConfig.Isolated)}' setting.  Must be 'true' or 'false'.");
                            continue;
                        }
                        feedConfig.Isolated = feedSetting;
                    }

                    string allowOverwriteOnFeed = fc.GetMetadata(nameof(FeedConfig.AllowOverwrite));
                    if (!string.IsNullOrEmpty(allowOverwriteOnFeed))
                    {
                        if (!bool.TryParse(allowOverwriteOnFeed, out bool feedSetting))
                        {
                            Log.LogError($"Invalid feed config '{nameof(FeedConfig.AllowOverwrite)}' setting.  Must be 'true' or 'false'.");
                            continue;
                        }
                        feedConfig.AllowOverwrite = feedSetting;
                    }

                    string latestLinkShortUrlPrefix = fc.GetMetadata(nameof(FeedConfig.LatestLinkShortUrlPrefix));
                    if (!string.IsNullOrEmpty(latestLinkShortUrlPrefix))
                    {
                        // Verify other inputs are provided
                        if (string.IsNullOrEmpty(AkaMSClientId) ||
                            string.IsNullOrEmpty(AkaMSClientSecret) ||
                            string.IsNullOrEmpty(AkaMSTenant) ||
                            string.IsNullOrEmpty(AkaMsOwners) ||
                            string.IsNullOrEmpty(AkaMSCreatedBy))
                        {
                            Log.LogError($"If a short url path is provided, please provide {nameof(AkaMSClientId)}, {nameof(AkaMSClientSecret)}, " +
                                $"{nameof(AkaMSTenant)}, {nameof(AkaMsOwners)}, {nameof(AkaMSCreatedBy)}");
                            continue;
                        }
                        feedConfig.LatestLinkShortUrlPrefix = latestLinkShortUrlPrefix;

                        // Set up the link manager if it hasn't already been done
                        if (_linkManager == null)
                        {
                            _linkManager = new AkaMSLinkManager(AkaMSClientId, AkaMSClientSecret, AkaMSTenant, Log);
                        }
                    }

                    string categoryKey = fc.ItemSpec.Trim().ToUpper();
                    if (!FeedConfigs.TryGetValue(categoryKey, out var feedsList))
                    {
                        FeedConfigs[categoryKey] = new List<FeedConfig>();
                    }
                    FeedConfigs[categoryKey].Add(feedConfig);
                }
            }
        }

        /// <summary>
        /// Adds `feedConfig.TargetURL` to the list of locations of the asset pointed by `assetRecord`.
        /// </summary>
        /// <param name="client">Maestro++ API client</param>
        /// <param name="assetRecord">Asset that will have location list updated</param>
        /// <param name="feedConfig">Configuration of where the asset was published</param>
        /// <param name="assetLocationType">Type of feed location that is being added</param>
        /// <returns>Whether the location was added to the list or not.</returns>
        private async Task<bool> TryAddAssetLocationAsync(IMaestroApi client, Asset assetRecord, FeedConfig feedConfig, AddAssetLocationToAssetAssetLocationType assetLocationType)
        {
            try
            {
                await client.Assets.AddAssetLocationToAssetAsync(assetRecord.Id, assetLocationType, feedConfig.TargetURL);
            }
            catch (RestApiException e) when (e.Response.StatusCode == System.Net.HttpStatusCode.NotModified)
            {
                // This is likely due to some concurrent operation trying to add same location to same asset.
                // This is not frequent, but it's possible, for instance, when a build publishes to multiple 
                // channels that target same feeds.
                Log.LogMessage($"Asset with Id {assetRecord.Id}, Version {assetRecord.Version} already has location {feedConfig.TargetURL}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Protect against accidental publishing of internal assets to non-internal feeds.
        /// </summary>
        /// <returns></returns>
        private void CheckForInternalBuildsOnPublicFeeds(FeedConfig feedConfig)
        {
            // If separated out for clarity.
            if (!SkipSafetyChecks)
            {
                if (InternalBuild && !feedConfig.Internal)
                {
                    Log.LogError($"Use of non-internal feed '{feedConfig.TargetURL}' is invalid for an internal build. This can be overridden with '{nameof(SkipSafetyChecks)}= true'");
                }
            }
        }

        /// <summary>
        ///     Determine whether the feed is public or private.
        /// </summary>
        /// <param name="feedUrl">Feed url to test</param>
        /// <returns>True if the feed is public, false if it is private, and null if it was not possible to determine.</returns>
        /// <remarks>
        /// Do an unauthenticated GET on the feed URL. If it succeeds, the feed is not public.
        /// If it fails with a 4* error, assume it is internal.
        /// </remarks>
        private async Task<bool?> IsFeedPublicAsync(string feedUrl, HttpClient httpClient)
        {
            bool? isPublic = null;

            bool success = await RetryHandler.RunAsync(async attempt =>
            {
                try
                {
                    HttpResponseMessage response = await httpClient.GetAsync(feedUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        isPublic = true;
                        return true;
                    }
                    else if (response.StatusCode >= (System.Net.HttpStatusCode)400 &&
                              response.StatusCode < (System.Net.HttpStatusCode)500)
                    {
                        isPublic = false;
                        return true;
                    }
                    else
                    {
                        // Don't know for certain, retry
                        return false;
                    }
                }
                catch (Exception e)
                {
                    Log.LogMessage(MessageImportance.Low, $"Unexpected exception {e.Message} when attempting to determine whether feed is internal.");
                    return false;
                }
            });

            if (!success)
            {
                // We couldn't determine anything.  We'd be unlikely to be able to push to this feed either,
                // since it's 5xx'ing.
                Log.LogError($"Unable to determine whether '{feedUrl}' is public or internal.");
            }

            return isPublic;
        }

        /// <summary>
        ///     Handle package publishing for all the feed configs.
        /// </summary>
        /// <param name="client">Maestro API client</param>
        /// <param name="buildAssets">Assets information about build being published.</param>
        /// <returns>Task</returns>
        private async Task HandlePackagePublishingAsync(IMaestroApi client, Dictionary<string, List<Asset>> buildAssets)
        {
            List<Task> publishTasks = new List<Task>();

            foreach (var packagesPerCategory in PackagesByCategory)
            {
                var category = packagesPerCategory.Key;
                var packages = packagesPerCategory.Value;

                if (FeedConfigs.TryGetValue(category, out List<FeedConfig> feedConfigsForCategory))
                {
                    foreach (var feedConfig in feedConfigsForCategory)
                    {
                        List<PackageArtifactModel> filteredPackages = FilterPackages(packages, feedConfig);

                        foreach (var package in filteredPackages)
                        {
                            string isolatedString = feedConfig.Isolated ? "Isolated" : "Non-Isolated";
                            string internalString = feedConfig.Internal ? $", Internal" : ", Public";
                            string shippingString = package.NonShipping ? "NonShipping" : "Shipping";
                            Log.LogMessage(MessageImportance.High, $"{package.Id}@{package.Version} ({shippingString}) -> {feedConfig.TargetURL} ({isolatedString}{internalString})");
                        }

                        switch (feedConfig.Type)
                        {
                            case FeedType.AzDoNugetFeed:
                                publishTasks.Add(PublishPackagesToAzDoNugetFeedAsync(filteredPackages, client, buildAssets, feedConfig));
                                break;
                            case FeedType.AzureStorageFeed:
                                publishTasks.Add(PublishPackagesToAzureStorageNugetFeedAsync(filteredPackages, client, buildAssets, feedConfig));
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

            await Task.WhenAll(publishTasks);
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

        private async Task HandleBlobPublishingAsync(IMaestroApi client, Dictionary<string, List<Asset>> buildAssets)
        {
            List<Task> publishTasks = new List<Task>();

            foreach (var blobsPerCategory in BlobsByCategory)
            {
                var category = blobsPerCategory.Key;
                var blobs = blobsPerCategory.Value;

                if (FeedConfigs.TryGetValue(category, out List<FeedConfig> feedConfigsForCategory))
                {
                    foreach (var feedConfig in feedConfigsForCategory)
                    {
                        List<BlobArtifactModel> filteredBlobs = FilterBlobs(blobs, feedConfig);

                        foreach (var blob in filteredBlobs)
                        {
                            string isolatedString = feedConfig.Isolated ? "Isolated" : "Non-Isolated";
                            string internalString = feedConfig.Internal ? $", Internal" : ", Public";
                            string shippingString = blob.NonShipping ? "NonShipping" : "Shipping";
                            Log.LogMessage(MessageImportance.High, $"{blob.Id} ({shippingString}) -> {feedConfig.TargetURL} ({isolatedString}{internalString})");
                        }

                        switch (feedConfig.Type)
                        {
                            case FeedType.AzDoNugetFeed:
                                publishTasks.Add(PublishBlobsToAzDoNugetFeedAsync(filteredBlobs, client, buildAssets, feedConfig));
                                break;
                            case FeedType.AzureStorageFeed:
                                publishTasks.Add(PublishBlobsToAzureStorageNugetFeedAsync(filteredBlobs, client, buildAssets, feedConfig));
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

            await Task.WhenAll(publishTasks);
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
        public void SplitArtifactsInCategories(BuildModel buildModel)
        {
            foreach (var packageAsset in buildModel.Artifacts.Packages)
            {
                string categories = string.Empty;

                if (!packageAsset.Attributes.TryGetValue("Category", out categories))
                {
                    // Package artifacts don't have extensions. They are always nupkgs.
                    // Set the category explicitly to "PACKAGE"
                    categories = PackagesCategory;
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
            Dictionary<string, List<Asset>> buildAssets,
            FeedConfig feedConfig)
        {
            await PushNugetPackagesAsync(packagesToPublish, feedConfig, maxClients: MaxClients,
                async (feed, httpClient, package, feedAccount, feedVisibility, feedName) =>
                {
                    string localPackagePath = Path.Combine(PackageAssetsBasePath, $"{package.Id}.{package.Version}.nupkg");
                    if (!File.Exists(localPackagePath))
                    {
                        Log.LogError($"Could not locate '{package.Id}.{package.Version}' at '{localPackagePath}'");
                        return;
                    }

                    Asset assetRecord = LookupAsset(package.Id, package.Version, buildAssets);
                    if (assetRecord == null)
                    {
                        Log.LogError($"Asset with Id {package.Id}, Version {package.Version} isn't registered on the BAR Build with ID {BARBuildId}");
                    }

                    await Task.WhenAll(new Task[] {
                        TryAddAssetLocationAsync(client, assetRecord, feedConfig, AddAssetLocationToAssetAssetLocationType.NugetFeed),
                        PushNugetPackageAsync(feed, httpClient, localPackagePath, package.Id, package.Version, feedAccount, feedVisibility, feedName),
                    });
                });
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
        public async Task PushNugetPackagesAsync<T>(List<T> packagesToPublish, FeedConfig feedConfig, int maxClients,
            Func<FeedConfig, HttpClient, T, string, string, string, Task> packagePublishAction)
        {
            if (!packagesToPublish.Any())
            {
                return;
            }

            var parsedUri = Regex.Match(feedConfig.TargetURL, PublishArtifactsInManifest.AzDoNuGetFeedPattern);
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
                    httpClient.Timeout = TimeSpan.FromSeconds(180);
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                        "Basic",
                        Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", feedConfig.Token))));

                    Log.LogMessage(MessageImportance.High, $"Pushing {packagesToPublish.Count()} packages.");
                    await System.Threading.Tasks.Task.WhenAll(packagesToPublish.Select(async packageToPublish =>
                    {
                        try
                        {
                            // Wait to avoid starting too many processes.
                            await clientThrottle.WaitAsync();
                            await packagePublishAction(feedConfig, httpClient, packageToPublish, feedAccount, feedVisibility, feedName);
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
        /// <param name="feedConfig">Infos about the target feed</param>
        /// <param name="packageToPublish">Package to push</param>
        /// <returns>Task</returns>
        /// <remarks>
        ///     This method attempts to take the most efficient path to push the package.
        ///     
        ///     There are three cases:
        ///         - The package does not exist, and is pushed normally
        ///         - The package exists, and its contents may or may not be equivalent.
        ///         - Azure DevOps is having some issue and we didn't succeed to publish at first.
        ///         
        ///     The second case is by far the most common. So, we first attempt to push the 
        ///     package normally using nuget.exe. If this fails, this could mean any number of 
        ///     things (like failed auth). But in normal circumstances, this might mean the 
        ///     package already exists. This either means that we are attempting to push the 
        ///     same package, or attemtping to push a different package with the same id and 
        ///     version. The second case is an error, as azure devops feeds are immutable, 
        ///     the former is simply a case where we should continue onward.
        ///     
        ///     To handle the third case we rely on the call to compare file contents 
        ///     `IsLocalPackageIdenticalToFeedPackage` to return null - meaning that it got 
        ///     a 404 when looking up the file in the feed - to trigger a retry on the publish 
        ///     operation. This was implemented this way becase we didn't want to rely on 
        ///     parsing the output of the push operation - which does a call to `nuget.exe` 
        ///     behind the scenes.
        /// </remarks>
        private async Task PushNugetPackageAsync(FeedConfig feedConfig, HttpClient client, string localPackageLocation, string id, string version,
            string feedAccount, string feedVisibility, string feedName)
        {
            Log.LogMessage(MessageImportance.High, $"Pushing package '{localPackageLocation}' to feed {feedConfig.TargetURL}");

            try
            {
                /// true - Package exists on the feed AND is identical to local one.
                /// false - Package exists on the feed AND is not identical to local one.
                /// null - Package DOES NOT EXIST on the feed.
                bool? packageExistIsIdentical = null;

                const int maxNuGetPushAttempts = 3;
                const int delayInSecondsBetweenAttempts = 3;
                int attemptIndex = 0;

                do
                {
                    // The feed key when pushing to AzDo feeds is "AzureDevOps" (works with the credential helper).
                    int result = await StartProcessAsync(NugetPath, $"push \"{localPackageLocation}\" -Source \"{feedConfig.TargetURL}\" -NonInteractive -ApiKey AzureDevOps");

                    if (result == 0)
                    {
                        break;
                    }

                    Log.LogMessage(MessageImportance.Low, $"Failed to push {localPackageLocation}, attempting to determine whether the package already exists on the feed with the same content.");

                    string packageContentUrl = $"https://pkgs.dev.azure.com/{feedAccount}/{feedVisibility}_apis/packaging/feeds/{feedName}/nuget/packages/{id}/versions/{version}/content";
                    packageExistIsIdentical = await IsLocalPackageIdenticalToFeedPackage(localPackageLocation, packageContentUrl, client);

                    if (packageExistIsIdentical == true)
                    {
                        Log.LogMessage(MessageImportance.Normal, $"Package '{localPackageLocation}' already exists on '{feedConfig.TargetURL}' but has the same content. Skipping.");
                    }
                    else if (packageExistIsIdentical == false)
                    {
                        Log.LogError($"Package '{localPackageLocation}' already exists on '{feedConfig.TargetURL}' with different content.");
                    }
                    else
                    {
                        // packageExist == null, which means we didn't find the package on the feed
                        // will retry the push and check again
                        await Task.Delay(TimeSpan.FromSeconds(delayInSecondsBetweenAttempts))
                            .ConfigureAwait(false);
                    }
                }
                while (packageExistIsIdentical == null && attemptIndex++ <= maxNuGetPushAttempts);

                if (attemptIndex > maxNuGetPushAttempts)
                {
                    Log.LogError($"Failed to publish package '{id}@{version}' after {maxNuGetPushAttempts} attempts.");
                }
            }
            catch (Exception e)
            {
                Log.LogError($"Unexpected exception pushing package '{id}@{version}': {e.Message}");
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
        private async Task<bool?> IsLocalPackageIdenticalToFeedPackage(string localPackageFullPath, string packageContentUrl, HttpClient client)
        {
            Log.LogMessage($"Getting package content from {packageContentUrl} and comparing to {localPackageFullPath}");

            bool? result = null;

            bool success = await RetryHandler.RunAsync(async attempt =>
            {
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
                            result = false;
                            return true;
                        }

                        if (md5HeaderAvailable)
                        {
                            var localMD5 = AzureStorageUtils.CalculateMD5(localPackageFullPath);
                            if (!localMD5.Equals(md5.Single(), StringComparison.OrdinalIgnoreCase))
                            {
                                Log.LogMessage(MessageImportance.Low, $"Package '{localPackageFullPath}' has different MD5 hash than remote package '{packageContentUrl}'.");
                            }

                            result = true;
                            return true;
                        }

                        const int BufferSize = 64 * 1024;

                        // Otherwise, compare the streams
                        var remoteStream = await response.Content.ReadAsStreamAsync();
                        result = await CompareStreamsAsync(localFileStream, remoteStream, BufferSize);
                        return true;
                    }
                }
                // String based comparison because the status code isn't exposed in HttpRequestException
                // see here: https://github.com/dotnet/runtime/issues/23648
                catch (HttpRequestException e)
                {
                    if (e.Message.Contains("404 (Not Found)"))
                    {
                        result = null;
                        return true;
                    }

                    // Retry this. Could be an http client timeout, 500, etc.
                    return false;
                }
            });

            return result;
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
            Dictionary<string, List<Asset>> buildAssets,
            FeedConfig feedConfig)
        {
            List<BlobArtifactModel> packagesToPublish = new List<BlobArtifactModel>();

            foreach (var blob in blobsToPublish)
            {
                // Applies to symbol packages and core-sdk's VS feed packages
                if (blob.Id.EndsWith(PackageSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    packagesToPublish.Add(blob);
                }
                else
                {
                    Log.LogWarning($"AzDO feed publishing not available for blobs. Blob '{blob.Id}' was not published.");
                }
            }

            await PushNugetPackagesAsync<BlobArtifactModel>(packagesToPublish, feedConfig, maxClients: MaxClients,
                async (feed, httpClient, blob, feedAccount, feedVisibility, feedName) =>
                {
                    // Lookup just by name
                    Asset assetRecord = LookupAsset(blob.Id, buildAssets);
                    if (assetRecord == null)
                    {
                        Log.LogError($"Asset with Id {blob.Id} isn't registered on the BAR Build with ID {BARBuildId}");
                    }
                    // Only attempt to push if the package doesn't have
                    // that location already.
                    else if (await TryAddAssetLocationAsync(client, assetRecord, feedConfig, AddAssetLocationToAssetAssetLocationType.Container))
                    {
                        // Determine the local path to the blob
                        string fileName = Path.GetFileName(blob.Id);
                        string localBlobPath = Path.Combine(BlobAssetsBasePath, fileName);
                        if (!File.Exists(localBlobPath))
                        {
                            Log.LogError($"Could not locate '{blob.Id} at '{localBlobPath}'");
                            return;
                        }

                        string id;
                        string version;
                        // Determine package ID and version by asking the nuget libraries
                        using (var packageReader = new NuGet.Packaging.PackageArchiveReader(localBlobPath))
                        {
                            PackageIdentity packageIdentity = packageReader.GetIdentity();
                            id = packageIdentity.Id;
                            version = packageIdentity.Version.ToString();
                        }

                        await PushNugetPackageAsync(feed, httpClient, localBlobPath, id, version, feedAccount, feedVisibility, feedName);
                    }
                });
        }

        private async Task PublishPackagesToAzureStorageNugetFeedAsync(
            List<PackageArtifactModel> packagesToPublish,
            IMaestroApi client,
            Dictionary<string, List<Asset>> buildAssets,
            FeedConfig feedConfig)
        {
            var packages = packagesToPublish.Select(p =>
            {
                var localPackagePath = Path.Combine(PackageAssetsBasePath, $"{p.Id}.{p.Version}.nupkg");
                if (!File.Exists(localPackagePath))
                {
                    Log.LogError($"Could not locate '{p.Id}.{p.Version}' at '{localPackagePath}'");
                }
                return localPackagePath;
            });

            if (Log.HasLoggedErrors)
            {
                return;
            }

            var blobFeedAction = CreateBlobFeedAction(feedConfig);

            var pushOptions = new PushOptions
            {
                AllowOverwrite = feedConfig.AllowOverwrite,
                PassIfExistingItemIdentical = true
            };

            await Task.WhenAll(new Task[]
            {
                Task.WhenAll(packagesToPublish.Select(async package =>
                {
                    Asset assetRecord = LookupAsset(package.Id, package.Version, buildAssets);
                    if (assetRecord == null)
                    {
                        Log.LogError($"Asset with Id {package.Id}, Version {package.Version} isn't registered on the BAR Build with ID {BARBuildId}");
                    }
                    else
                    {
                        await TryAddAssetLocationAsync(client, assetRecord, feedConfig, AddAssetLocationToAssetAssetLocationType.NugetFeed);
                    }
                })),
                blobFeedAction.PushToFeedAsync(packages, pushOptions)
            });
        }

        private async Task PublishBlobsToAzureStorageNugetFeedAsync(
            List<BlobArtifactModel> blobsToPublish,
            IMaestroApi client,
            Dictionary<string, List<Asset>> buildAssets,
            FeedConfig feedConfig)
        {
            var blobs = blobsToPublish
                .Select(blob =>
                {
                    var fileName = Path.GetFileName(blob.Id);
                    var localBlobPath = Path.Combine(BlobAssetsBasePath, fileName);
                    if (!File.Exists(localBlobPath))
                    {
                        Log.LogError($"Could not locate '{blob.Id} at '{localBlobPath}'");
                    }

                    return new MSBuild.TaskItem(localBlobPath, new Dictionary<string, string>
                    {
                        {"RelativeBlobPath", blob.Id}
                    });
                })
                .ToArray();

            if (Log.HasLoggedErrors)
            {
                return;
            }

            var blobFeedAction = CreateBlobFeedAction(feedConfig);
            var pushOptions = new PushOptions
            {
                AllowOverwrite = feedConfig.AllowOverwrite,
                PassIfExistingItemIdentical = true
            };

            await Task.WhenAll(new Task[]
                {
                    Task.WhenAll(blobsToPublish.Select(async blob =>
                    {
                        Asset assetRecord = LookupAsset(blob.Id, buildAssets);

                        if (assetRecord == null)
                        {
                            Log.LogError($"Asset with Id {blob.Id} isn't registered on the BAR Build with ID {BARBuildId}");
                        }
                        else
                        {
                            await TryAddAssetLocationAsync(client, assetRecord, feedConfig, AddAssetLocationToAssetAssetLocationType.Container);
                        }
                    })),
                    blobFeedAction.PublishToFlatContainerAsync(blobs, maxClients: MaxClients, pushOptions)
                }
            );

            // The latest links should be updated only after the publishing is complete, to avoid
            // dead links in the interim.
            await CreateOrUpdateLatestLinksAsync(blobsToPublish, feedConfig);
        }

        private async Task CreateOrUpdateLatestLinksAsync(List<BlobArtifactModel> blobsToPublish, FeedConfig feedConfig)
        {
            if (string.IsNullOrEmpty(feedConfig.LatestLinkShortUrlPrefix))
            {
                return;
            }

            Log.LogMessage(MessageImportance.High, "The following aka.ms links for blobs will be created:");
            IEnumerable<AkaMSLink> linksToCreate = blobsToPublish.Select(blob =>
            {
                // Strip away the feed expected suffix (index.json) and append on the
                // blob path.
                string actualTargetUrl = feedConfig.TargetURL.Substring(0,
                    feedConfig.TargetURL.Length - ExpectedFeedUrlSuffix.Length) + blob.Id;

                AkaMSLink newLink = new AkaMSLink
                {
                    ShortUrl = GetLatestShortUrlForBlob(feedConfig, blob),
                    TargetUrl = actualTargetUrl
                };
                Log.LogMessage(MessageImportance.High, $"  aka.ms/{newLink.ShortUrl} -> {newLink.TargetUrl}");
                return newLink;
            });

            await _linkManager.CreateOrUpdateLinksAsync(linksToCreate, AkaMsOwners, AkaMSCreatedBy, AkaMSGroupOwner, true);
        }

        /// <summary>
        ///     Get the short url for a blob.
        /// </summary>
        /// <param name="feedConfig">Feed configuration</param>
        /// <param name="blob">Blob</param>
        /// <returns>Short url prefix for the blob.</returns>
        /// <remarks>
        public string GetLatestShortUrlForBlob(FeedConfig feedConfig, BlobArtifactModel blob)
        {
            string blobIdWithoutVersions = VersionIdentifier.RemoveVersions(blob.Id);

            return Path.Combine(feedConfig.LatestLinkShortUrlPrefix, blobIdWithoutVersions).Replace("\\", "/");
        }

        private BlobFeedAction CreateBlobFeedAction(FeedConfig feedConfig)
        {
            var proxyBackedFeedMatch = Regex.Match(feedConfig.TargetURL, AzureStorageProxyFeedPattern);
            var proxyBackedStaticFeedMatch = Regex.Match(feedConfig.TargetURL, AzureStorageProxyFeedStaticPattern);
            var azureStorageStaticBlobFeedMatch = Regex.Match(feedConfig.TargetURL, AzureStorageStaticBlobFeedPattern);

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
                    ConnectionString = $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={feedConfig.Token};EndpointSuffix=core.windows.net"
                };

                return new BlobFeedAction(sleetSource, feedConfig.Token, Log);
            }
            else if (azureStorageStaticBlobFeedMatch.Success)
            {
                return new BlobFeedAction(feedConfig.TargetURL, feedConfig.Token, Log);
            }
            else
            {
                Log.LogError($"Could not parse Azure feed URL: '{feedConfig.TargetURL}'");
                return null;
            }
        }

        /// <summary>
        ///     Infers the category based on the extension of the particular asset
        ///     
        ///     If no category can be inferred, then "OTHER" is used.
        /// </summary>
        /// <param name="assetId">ID of asset</param>
        /// <returns>Asset cateogry</returns>
        private string InferCategory(string assetId)
        {
            var extension = Path.GetExtension(assetId).ToUpper();

            var whichCategory = new Dictionary<string, string>()
            {
                { ".NUPKG", PackagesCategory },
                { ".PKG", "OSX" },
                { ".DEB", "DEB" },
                { ".RPM", "RPM" },
                { ".NPM", "NODE" },
                { ".ZIP", "BINARYLAYOUT" },
                { ".MSI", "INSTALLER" },
                { ".SHA", "CHECKSUM" },
                { ".SHA512", "CHECKSUM" },
                { ".POM", "MAVEN" },
                { ".VSIX", "VSIX" },
                { ".CAB", "BINARYLAYOUT" },
                { ".TAR", "BINARYLAYOUT" },
                { ".GZ", "BINARYLAYOUT" },
                { ".TGZ", "BINARYLAYOUT" },
                { ".EXE", "INSTALLER" },
                { ".SVG", "BADGE"},
                { ".WIXLIB", "INSTALLER" },
                { ".WIXPDB", "INSTALLER" },
                { ".JAR", "INSTALLER" },
                { ".VERSION", "INSTALLER"},
                { ".SWR", "INSTALLER" }
            };

            if (whichCategory.TryGetValue(extension, out var category))
            {
                // Special handling for symbols.nupkg. There are typically plenty of
                // periods in package names. We get the extension to identify nupkg
                // assets. But symbol packages have the extension '.symbols.nupkg'.
                // We want to divide these into a separate category because for stabilized builds,
                // they should go to an isolated location. In a non-stabilized build, they can go straight
                // to blob feeds because the blob feed push tasks will automatically push them to the assets.
                if (assetId.EndsWith(SymbolPackageSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    return "SYMBOLS";
                }
                return category;
            }
            else
            {
                Log.LogMessage(MessageImportance.High, $"Defaulting to category 'OTHER' for asset {assetId}");
                return "OTHER";
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
        public string TargetURL { get; set; }
        public FeedType Type { get; set; }
        public string Token { get; set; }
        public AssetSelection AssetSelection { get; set; } = AssetSelection.All;
        /// <summary>
        /// If true, the feed is treated as 'isolated', meaning nuget packages pushed
        /// to it may be stable.
        /// </summary>
        public bool Isolated { get; set; } = false;
        /// <summary>
        /// If true, the feed is treated as 'internal', meaning artifacts from an internal build
        /// can be published here.
        /// </summary>
        public bool Internal { get; set; } = false;
        /// <summary>
        /// If true, the items on the feed can be overwritten. This is only
        /// valid for azure blob storage feeds.
        /// </summary>
        public bool AllowOverwrite { get; set; } = false;
        /// <summary>
        /// Prefix of aka.ms links that should be generated for blobs.
        /// Not applicable to packages
        /// Generates a link the blob, stripping away any version information in the file or blob path.
        /// E.g. 
        ///      [LatestLinkShortUrlPrefix]/aspnetcore/Runtime/dotnet-hosting-win.exe -> aspnetcore/Runtime/3.1.0-preview2.19511.6/dotnet-hosting-3.1.0-preview2.19511.6-win.exe
        /// </summary>
        public string LatestLinkShortUrlPrefix { get; set; }
    }
}
