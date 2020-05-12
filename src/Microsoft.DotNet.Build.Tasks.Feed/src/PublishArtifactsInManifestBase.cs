// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
using Microsoft.DotNet.Maestro.Client;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    /// <summary>
    /// The intended use of this task is to push artifacts described in
    /// a build manifest to a static package feed.
    /// </summary>
    public abstract class PublishArtifactsInManifestBase : Microsoft.Build.Utilities.Task
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
        public bool InternalBuild { get; set; }

        /// <summary>
        /// If true, safety checks only print messages and do not error
        /// - Internal asset to public feed
        /// - Stable packages to non-isolated feeds
        /// </summary>
        public bool SkipSafetyChecks { get; set; } = false;

        #region Information for AKA MS link generation
        public string AkaMSClientId { get; set; }
        public string AkaMSClientSecret { get; set; }
        public string AkaMSTenant { get; set; }
        public string AkaMsOwners { get; set; }
        public string AkaMSCreatedBy { get; set; }
        public string AkaMSGroupOwner { get; set; }
        #endregion

        #region Target Channel Configs
        private const string FeedGeneralTesting = "https://pkgs.dev.azure.com/dnceng/public/_packaging/general-testing/nuget/v3/index.json";
        private const string FeedGeneralTestingSymbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/general-testing-symbols/nuget/v3/index.json";

        private const string FeedDotNetExperimental = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-experimental/nuget/v3/index.json";
        private const string FeedDotNetExperimentalSymbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-experimental-symbols/nuget/v3/index.json";

        private const string FeedDotNetEngShipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json";
        private const string FeedDotNetEngTransport = FeedDotNetEngShipping;
        private const string FeedDotNetEngSymbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng-symbols/nuget/v3/index.json";

        private const string FeedDotNetToolsShipping = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet-tools/nuget/v3/index.json";
        private const string FeedDotNetToolsTransport = FeedDotNetToolsShipping;
        private const string FeedDotNetToolsSymbols = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet-tools-symbols/nuget/v3/index.json";

        private const string FeedDotNetToolsInternalShipping = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet-tools-internal/nuget/v3/index.json";
        private const string FeedDotNetToolsInternalTransport = FeedDotNetToolsInternalShipping;
        private const string FeedDotNetToolsInternalSymbols = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet-tools-internal-symbols/nuget/v3/index.json";

        private const string FeedDotNet31Shipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1/nuget/v3/index.json";
        private const string FeedDotNet31Transport = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-transport/nuget/v3/index.json";
        private const string FeedDotNet31Symbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-symbols/nuget/v3/index.json";

        private const string FeedDotNet31InternalShipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-internal/nuget/v3/index.json";
        private const string FeedDotNet31InternalTransport = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-internal-transport/nuget/v3/index.json";
        private const string FeedDotNet31InternalSymbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-internal-symbols/nuget/v3/index.json";

        private const string FeedDotNet5Shipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet5/nuget/v3/index.json";
        private const string FeedDotNet5Transport = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet5-transport/nuget/v3/index.json";
        private const string FeedDotNet5Symbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet5-symbols/nuget/v3/index.json";

        protected List<TargetChannelConfig> ChannelInfos = new List<TargetChannelConfig>() {
            new TargetChannelConfig(
                131,
                PublishingInfraVersion.All,
                ".NET 5 Dev",
                "net5/dev",
                FeedDotNet5Shipping,
                FeedDotNet5Transport,
                FeedDotNet5Symbols),

            new TargetChannelConfig(
                739,
                PublishingInfraVersion.All,
                ".NET 5 Preview 3",
                "net5/preview3",
                FeedDotNet5Shipping,
                FeedDotNet5Transport,
                FeedDotNet5Symbols),

            new TargetChannelConfig(
                856,
                PublishingInfraVersion.All,
                ".NET 5 Preview 4",
                "net5/preview4",
                FeedDotNet5Shipping,
                FeedDotNet5Transport,
                FeedDotNet5Symbols),

            new TargetChannelConfig(
                857,
                PublishingInfraVersion.All,
                ".NET 5 Preview 5",
                "net5/preview5",
                FeedDotNet5Shipping,
                FeedDotNet5Transport,
                FeedDotNet5Symbols),

            new TargetChannelConfig(
                2,
                PublishingInfraVersion.All,
                ".NET Eng - Latest",
                "eng/daily",
                FeedDotNetEngShipping,
                FeedDotNetEngTransport,
                FeedDotNetEngSymbols),

            new TargetChannelConfig(
                9,
                PublishingInfraVersion.All,
                ".NET Eng - Validation",
                "eng/validation",
                FeedDotNetEngShipping,
                FeedDotNetEngTransport,
                FeedDotNetEngSymbols),

            new TargetChannelConfig(
                529,
                PublishingInfraVersion.All,
                "General Testing",
                "generaltesting",
                FeedGeneralTesting,
                FeedGeneralTesting,
                FeedGeneralTestingSymbols),

            new TargetChannelConfig(
                548,
                PublishingInfraVersion.All,
                ".NET Core Tooling Dev",
                akaMSChannelName: string.Empty,
                FeedDotNetToolsShipping,
                FeedDotNetToolsTransport,
                FeedDotNetToolsSymbols),

            new TargetChannelConfig(
                549,
                PublishingInfraVersion.All,
                ".NET Core Tooling Release",
                akaMSChannelName: string.Empty,
                FeedDotNetToolsShipping,
                FeedDotNetToolsTransport,
                FeedDotNetToolsSymbols),

            new TargetChannelConfig(
                551,
                PublishingInfraVersion.All,
                ".NET Internal Tooling",
                akaMSChannelName: string.Empty,
                FeedDotNetToolsInternalShipping,
                FeedDotNetToolsInternalTransport,
                FeedDotNetToolsInternalSymbols),

            new TargetChannelConfig(
                562,
                PublishingInfraVersion.All,
                ".NET Core Experimental",
                akaMSChannelName: string.Empty,
                FeedDotNetExperimental,
                FeedDotNetExperimental,
                FeedDotNetExperimentalSymbols),

            new TargetChannelConfig(
                678,
                PublishingInfraVersion.All,
                ".NET Eng Services - Int",
                akaMSChannelName: string.Empty,
                FeedDotNetEngShipping,
                FeedDotNetEngTransport,
                FeedDotNetEngSymbols),

            new TargetChannelConfig(
                679,
                PublishingInfraVersion.All,
                ".NET Eng Services - Prod",
                akaMSChannelName: string.Empty,
                FeedDotNetEngShipping,
                FeedDotNetEngTransport,
                FeedDotNetEngSymbols),

            new TargetChannelConfig(
                921,
                PublishingInfraVersion.All,
                ".NET Core SDK 3.1.4xx",
                akaMSChannelName: string.Empty,
                FeedDotNet31Shipping,
                FeedDotNet31Transport,
                FeedDotNet31Symbols),

            new TargetChannelConfig(
                922,
                PublishingInfraVersion.All,
                ".NET Core SDK 3.1.4xx Internal",
                akaMSChannelName: string.Empty,
                FeedDotNet31InternalShipping,
                FeedDotNet31InternalTransport,
                FeedDotNet31InternalSymbols),

            new TargetChannelConfig(
                759,
                PublishingInfraVersion.All,
                ".NET Core SDK 3.1.3xx",
                akaMSChannelName: string.Empty,
                FeedDotNet31Shipping,
                FeedDotNet31Transport,
                FeedDotNet31Symbols),

            new TargetChannelConfig(
                760,
                PublishingInfraVersion.All,
                ".NET Core SDK 3.1.3xx Internal",
                akaMSChannelName: string.Empty,
                FeedDotNet31InternalShipping,
                FeedDotNet31InternalTransport,
                FeedDotNet31InternalSymbols),
        };
        #endregion

        public readonly Dictionary<string, List<TargetFeedConfig>> FeedConfigs = new Dictionary<string, List<TargetFeedConfig>>();

        private readonly Dictionary<string, List<PackageArtifactModel>> PackagesByCategory = new Dictionary<string, List<PackageArtifactModel>>();

        private readonly Dictionary<string, List<BlobArtifactModel>> BlobsByCategory = new Dictionary<string, List<BlobArtifactModel>>();

        private HashSet<(int AssetId, string AssetLocation, AddAssetLocationToAssetAssetLocationType LocationType)> NewAssetLocations =
            new HashSet<(int AssetId, string AssetLocation, AddAssetLocationToAssetAssetLocationType LocationType)>();

        private LatestLinksManager LinkManager { get; set; } = null;

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public abstract Task<bool> ExecuteAsync();

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
        protected Dictionary<string, List<Asset>> CreateBuildAssetDictionary(Maestro.Client.Models.Build buildInformation)
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
                    string targetFeedUrl = fc.GetMetadata(nameof(Model.TargetFeedConfig.TargetURL));
                    string feedKey = fc.GetMetadata(nameof(Model.TargetFeedConfig.Token));
                    string type = fc.GetMetadata(nameof(Model.TargetFeedConfig.Type));

                    if (string.IsNullOrEmpty(targetFeedUrl) ||
                        string.IsNullOrEmpty(feedKey) ||
                        string.IsNullOrEmpty(type))
                    {
                        Log.LogError($"Invalid FeedConfig entry. {nameof(Model.TargetFeedConfig.TargetURL)}='{targetFeedUrl}' {nameof(Model.TargetFeedConfig.Type)}='{type}' {nameof(Model.TargetFeedConfig.Token)}='{feedKey}'");
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

                    var feedConfig = new TargetFeedConfig()
                    {
                        TargetURL = targetFeedUrl,
                        Type = feedType,
                        Token = feedKey
                    };

                    string assetSelection = fc.GetMetadata(nameof(Model.TargetFeedConfig.AssetSelection));
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
                    string feedIsInternal = fc.GetMetadata(nameof(Model.TargetFeedConfig.Internal));
                    if (!string.IsNullOrEmpty(feedIsInternal))
                    {
                        if (!bool.TryParse(feedIsInternal, out bool feedSetting))
                        {
                            Log.LogError($"Invalid feed config '{nameof(Model.TargetFeedConfig.Internal)}' setting.  Must be 'true' or 'false'.");
                            continue;
                        }
                        feedConfig.Internal = feedSetting;
                    }
                    else
                    {
                        bool? isPublicFeed = await GeneralUtils.IsFeedPublicAsync(feedConfig.TargetURL, httpClient, Log);
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

                    string feedIsIsolated = fc.GetMetadata(nameof(Model.TargetFeedConfig.Isolated));
                    if (!string.IsNullOrEmpty(feedIsIsolated))
                    {
                        if (!bool.TryParse(feedIsIsolated, out bool feedSetting))
                        {
                            Log.LogError($"Invalid feed config '{nameof(Model.TargetFeedConfig.Isolated)}' setting.  Must be 'true' or 'false'.");
                            continue;
                        }
                        feedConfig.Isolated = feedSetting;
                    }

                    string allowOverwriteOnFeed = fc.GetMetadata(nameof(Model.TargetFeedConfig.AllowOverwrite));
                    if (!string.IsNullOrEmpty(allowOverwriteOnFeed))
                    {
                        if (!bool.TryParse(allowOverwriteOnFeed, out bool feedSetting))
                        {
                            Log.LogError($"Invalid feed config '{nameof(Model.TargetFeedConfig.AllowOverwrite)}' setting.  Must be 'true' or 'false'.");
                            continue;
                        }
                        feedConfig.AllowOverwrite = feedSetting;
                    }

                    string latestLinkShortUrlPrefix = fc.GetMetadata(nameof(Model.TargetFeedConfig.LatestLinkShortUrlPrefix));
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
                        if (LinkManager == null)
                        {
                            LinkManager = new LatestLinksManager(AkaMSClientId, AkaMSClientSecret, AkaMSTenant, AkaMSGroupOwner, AkaMSCreatedBy, AkaMsOwners, Log);
                        }
                    }

                    string categoryKey = fc.ItemSpec.Trim().ToUpper();
                    if (!FeedConfigs.TryGetValue(categoryKey, out _))
                    {
                        FeedConfigs[categoryKey] = new List<TargetFeedConfig>();
                    }
                    FeedConfigs[categoryKey].Add(feedConfig);
                }
            }
        }

        /// <summary>
        ///   Records the association of Asset -> AssetLocation to be persisted later in BAR.
        /// </summary>
        /// <param name="assetId">Id of the asset (i.e., name of the package) whose the location should be updated.</param>
        /// <param name="assetVersion">Version of the asset whose the location should be updated.</param>
        /// <param name="buildAssets">List of BAR Assets for the build that's being modified.</param>
        /// <param name="feedConfig">Configuration of where the asset was published.</param>
        /// <param name="assetLocationType">Type of feed location that is being added.</param>
        /// <returns>True if that asset didn't have the informed location recorded already.</returns>
        private bool TryAddAssetLocation(string assetId, string assetVersion, Dictionary<string, List<Asset>> buildAssets, TargetFeedConfig feedConfig, AddAssetLocationToAssetAssetLocationType assetLocationType)
        {
            Asset assetRecord = string.IsNullOrEmpty(assetVersion) ?
                LookupAsset(assetId, buildAssets) :
                LookupAsset(assetId, assetVersion, buildAssets);

            if (assetRecord == null)
            {
                string versionMsg = string.IsNullOrEmpty(assetVersion) ? string.Empty : $"and version {assetVersion}";
                Log.LogError($"Asset with Id {assetId} {versionMsg} isn't registered on the BAR Build with ID {BARBuildId}");
                return false;
            }

            return NewAssetLocations.Add((assetRecord.Id, feedConfig.TargetURL, assetLocationType));
        }

        /// <summary>
        ///   Persist in BAR all pending associations of Asset -> AssetLocation stored in `NewAssetLocations`.
        /// </summary>
        /// <param name="client">Maestro++ API client</param>
        protected Task PersistPendingAssetLocationAsync(IMaestroApi client)
        {
            var updates = NewAssetLocations.Select(nal => new AssetAndLocation(nal.AssetId, (LocationType)nal.LocationType)
            {
                Location = nal.AssetLocation
            }).ToImmutableList();

            return client.Assets.BulkAddLocationsAsync(updates);
        }

        /// <summary>
        /// Protect against accidental publishing of internal assets to non-internal feeds.
        /// </summary>
        /// <returns></returns>
        private void CheckForInternalBuildsOnPublicFeeds(TargetFeedConfig feedConfig)
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
        ///     Handle package publishing for all the feed configs.
        /// </summary>
        /// <param name="client">Maestro API client</param>
        /// <param name="buildAssets">Assets information about build being published.</param>
        /// <returns>Task</returns>
        protected async Task HandlePackagePublishingAsync(Dictionary<string, List<Asset>> buildAssets)
        {
            List<Task> publishTasks = new List<Task>();

            foreach (var packagesPerCategory in PackagesByCategory)
            {
                var category = packagesPerCategory.Key;
                var packages = packagesPerCategory.Value;

                if (FeedConfigs.TryGetValue(category, out List<TargetFeedConfig> feedConfigsForCategory))
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
                                publishTasks.Add(PublishPackagesToAzDoNugetFeedAsync(filteredPackages, buildAssets, feedConfig));
                                break;
                            case FeedType.AzureStorageFeed:
                                publishTasks.Add(PublishPackagesToAzureStorageNugetFeedAsync(filteredPackages, buildAssets, feedConfig));
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

        private List<PackageArtifactModel> FilterPackages(List<PackageArtifactModel> packages, TargetFeedConfig feedConfig)
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

        protected async Task HandleBlobPublishingAsync(Dictionary<string, List<Asset>> buildAssets)
        {
            List<Task> publishTasks = new List<Task>();

            foreach (var blobsPerCategory in BlobsByCategory)
            {
                var category = blobsPerCategory.Key;
                var blobs = blobsPerCategory.Value;

                if (FeedConfigs.TryGetValue(category, out List<TargetFeedConfig> feedConfigsForCategory))
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
                                publishTasks.Add(PublishBlobsToAzDoNugetFeedAsync(filteredBlobs, buildAssets, feedConfig));
                                break;
                            case FeedType.AzureStorageFeed:
                                publishTasks.Add(PublishBlobsToAzureStorageNugetFeedAsync(filteredBlobs, buildAssets, feedConfig));
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
        private List<BlobArtifactModel> FilterBlobs(List<BlobArtifactModel> blobs, TargetFeedConfig feedConfig)
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
                    categories = GeneralUtils.PackagesCategory;
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
                    categories = GeneralUtils.InferCategory(blobAsset.Id, Log);
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
            Dictionary<string, List<Asset>> buildAssets,
            TargetFeedConfig feedConfig)
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

                    TryAddAssetLocation(package.Id, package.Version, buildAssets, feedConfig, AddAssetLocationToAssetAssetLocationType.NugetFeed);

                    await PushNugetPackageAsync(feed, httpClient, localPackagePath, package.Id, package.Version, feedAccount, feedVisibility, feedName);
                });
        }

        /// <summary>
        ///     Push nuget packages to the azure devops feed.
        /// </summary>
        /// <param name="packagesToPublish">List of packages to publish</param>
        /// <param name="feedConfig">Information about feed to publish ot</param>
        /// <returns>Async task.</returns>
        public async Task PushNugetPackagesAsync<T>(List<T> packagesToPublish, TargetFeedConfig feedConfig, int maxClients,
            Func<TargetFeedConfig, HttpClient, T, string, string, string, Task> packagePublishAction)
        {
            if (!packagesToPublish.Any())
            {
                return;
            }

            var parsedUri = Regex.Match(feedConfig.TargetURL, PublishArtifactsInManifestV2.AzDoNuGetFeedPattern);
            if (!parsedUri.Success)
            {
                Log.LogError($"Azure DevOps NuGetFeed was not in the expected format '{PublishArtifactsInManifestV2.AzDoNuGetFeedPattern}'");
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
        private async Task PushNugetPackageAsync(TargetFeedConfig feedConfig, HttpClient client, string localPackageLocation, string id, string version,
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
                    int result = await GeneralUtils.StartProcessAsync(NugetPath, $"push \"{localPackageLocation}\" -Source \"{feedConfig.TargetURL}\" -NonInteractive -ApiKey AzureDevOps");

                    if (result == 0)
                    {
                        break;
                    }

                    Log.LogMessage(MessageImportance.Low, $"Failed to push {localPackageLocation}, attempting to determine whether the package already exists on the feed with the same content.");

                    string packageContentUrl = $"https://pkgs.dev.azure.com/{feedAccount}/{feedVisibility}_apis/packaging/feeds/{feedName}/nuget/packages/{id}/versions/{version}/content";
                    packageExistIsIdentical = await GeneralUtils.IsLocalPackageIdenticalToFeedPackage(localPackageLocation, packageContentUrl, client, Log);

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

        private async Task PublishBlobsToAzDoNugetFeedAsync(
            List<BlobArtifactModel> blobsToPublish,
            Dictionary<string, List<Asset>> buildAssets,
            TargetFeedConfig feedConfig)
        {
            List<BlobArtifactModel> packagesToPublish = new List<BlobArtifactModel>();

            foreach (var blob in blobsToPublish)
            {
                // Applies to symbol packages and core-sdk's VS feed packages
                if (blob.Id.EndsWith(GeneralUtils.PackageSuffix, StringComparison.OrdinalIgnoreCase))
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
                    if (TryAddAssetLocation(blob.Id, assetVersion: null, buildAssets, feedConfig, AddAssetLocationToAssetAssetLocationType.Container))
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
            Dictionary<string, List<Asset>> buildAssets,
            TargetFeedConfig feedConfig)
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

            packagesToPublish.Select(package => TryAddAssetLocation(package.Id, package.Version, buildAssets, feedConfig, AddAssetLocationToAssetAssetLocationType.NugetFeed));

            await blobFeedAction.PushToFeedAsync(packages, pushOptions);
        }

        private async Task PublishBlobsToAzureStorageNugetFeedAsync(
            List<BlobArtifactModel> blobsToPublish,
            Dictionary<string, List<Asset>> buildAssets,
            TargetFeedConfig feedConfig)
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

                    return new Microsoft.Build.Utilities.TaskItem(localBlobPath, new Dictionary<string, string>
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

            blobsToPublish.Select(blob => TryAddAssetLocation(blob.Id, assetVersion: null, buildAssets, feedConfig, AddAssetLocationToAssetAssetLocationType.Container));

            await blobFeedAction.PublishToFlatContainerAsync(blobs, maxClients: MaxClients, pushOptions);

            // The latest links should be updated only after the publishing is complete, to avoid
            // dead links in the interim.
            await LinkManager.CreateOrUpdateLatestLinksAsync(blobsToPublish, feedConfig, ExpectedFeedUrlSuffix.Length);
        }

        private BlobFeedAction CreateBlobFeedAction(TargetFeedConfig feedConfig)
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
    }
}
