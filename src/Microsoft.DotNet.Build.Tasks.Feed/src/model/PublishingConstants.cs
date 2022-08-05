// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System.Collections.Generic;
using System.ComponentModel;

namespace Microsoft.DotNet.Build.Tasks.Feed.Model
{
    public class PublishingConstants
    {
        public static readonly string LegacyDotNetBlobFeedURL = "https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json";
        public static readonly string ExpectedFeedUrlSuffix = "index.json";

        // Matches package feeds like
        // https://dotnet-feed-internal.azurewebsites.net/container/dotnet-core-internal/sig/dsdfasdfasdf234234s/se/2020-02-02/darc-int-dotnet-arcade-services-babababababe-08/index.json
        public static readonly string AzureStorageProxyFeedPattern =
            @"(?<feedURL>https://([a-z-]+).azurewebsites.net/container/(?<container>[^/]+)/sig/\w+/se/([0-9]{4}-[0-9]{2}-[0-9]{2})/(?<baseFeedName>darc-(?<type>int|pub)-(?<repository>.+?)-(?<sha>[A-Fa-f0-9]{7,40})-?(?<subversion>\d*)/))index.json";

        // Matches package feeds like the one below. Special case for static internal proxy-backed feed
        // https://dotnet-feed-internal.azurewebsites.net/container/dotnet-core-internal/sig/dsdfasdfasdf234234s/se/2020-02-02/darc-int-dotnet-arcade-services-babababababe-08/index.json
        public static readonly string AzureStorageProxyFeedStaticPattern =
            @"(?<feedURL>https://([a-z-]+).azurewebsites.net/container/(?<container>[^/]+)/sig/\w+/se/([0-9]{4}-[0-9]{2}-[0-9]{2})/(?<baseFeedName>[^/]+/))index.json";

        // Matches package feeds like
        // https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json
        public static readonly string AzureStorageStaticBlobFeedPattern =
            @"https://([a-z-]+).blob.core.windows.net/[^/]+/index.json";

        // Matches package feeds like
        // https://pkgs.dev.azure.com/dnceng/public/_packaging/public-feed-name/nuget/v3/index.json
        // or https://pkgs.dev.azure.com/dnceng/_packaging/internal-feed-name/nuget/v3/index.json
        public static readonly string AzDoNuGetFeedPattern =
            @"https://pkgs.dev.azure.com/(?<account>[a-zA-Z0-9-]+)/(?<visibility>[a-zA-Z0-9-]+/)?_packaging/(?<feed>.+)/nuget/v3/index.json";

        public static readonly TargetFeedContentType[] InstallersAndSymbols = {
            TargetFeedContentType.OSX,
            TargetFeedContentType.Deb,
            TargetFeedContentType.Rpm,
            TargetFeedContentType.Node,
            TargetFeedContentType.BinaryLayout,
            TargetFeedContentType.Installer,
            TargetFeedContentType.Maven,
            TargetFeedContentType.VSIX,
            TargetFeedContentType.Badge,
            TargetFeedContentType.Symbols,
            TargetFeedContentType.Other
        };

        public static readonly TargetFeedContentType[] InstallersAndChecksums = {
            TargetFeedContentType.OSX,
            TargetFeedContentType.Deb,
            TargetFeedContentType.Rpm,
            TargetFeedContentType.Node,
            TargetFeedContentType.BinaryLayout,
            TargetFeedContentType.Installer,
            TargetFeedContentType.Maven,
            TargetFeedContentType.VSIX,
            TargetFeedContentType.Badge,
            TargetFeedContentType.Checksum,
            TargetFeedContentType.Other
        };

        public enum BuildQuality
        {
            [Description("daily")]
            Daily,

            [Description("signed")]
            Signed,

            [Description("validated")]
            Validated,

            [Description("preview")]
            Preview,

            [Description("")]
            GA
        }

        #region Target Channel Config Feeds
        public const string FeedForChecksums = "https://dotnetclichecksums.blob.core.windows.net/dotnet/index.json";
        public const string FeedForInstallers = "https://dotnetcli.blob.core.windows.net/dotnet/index.json";

        private const string FeedInternalForChecksums = "https://dotnetclichecksumsmsrc.blob.core.windows.net/dotnet/index.json";
        public const string FeedInternalForInstallers = "https://dotnetclimsrc.blob.core.windows.net/dotnet/index.json";

        public const string FeedStagingForInstallers = "https://dotnetbuilds.blob.core.windows.net/public";
        public const string FeedStagingForChecksums = "https://dotnetbuilds.blob.core.windows.net/public-checksums";

        public const string FeedStagingInternalForInstallers = "https://dotnetbuilds.blob.core.windows.net/internal";
        public const string FeedStagingInternalForChecksums = "https://dotnetbuilds.blob.core.windows.net/internal-checksums";

        private const string FeedGeneralTesting = "https://pkgs.dev.azure.com/dnceng/public/_packaging/general-testing/nuget/v3/index.json";

        private const string FeedDotNetExperimental = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-experimental/nuget/v3/index.json";

        private const string FeedDotNetEng = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json";

        private const string FeedDotNetTools = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json";

        private const string FeedDotNetToolsInternal = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet-tools-internal/nuget/v3/index.json";

        private const string FeedDotNet31Shipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1/nuget/v3/index.json";
        private const string FeedDotNet31Transport = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-transport/nuget/v3/index.json";

        private const string FeedDotNet31InternalShipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-internal/nuget/v3/index.json";
        private const string FeedDotNet31InternalTransport = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-internal-transport/nuget/v3/index.json";

        private const string FeedDotNet31Blazor = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-blazor/nuget/v3/index.json";

        public const string FeedDotNet5Shipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet5/nuget/v3/index.json";
        public const string FeedDotNet5Transport = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet5-transport/nuget/v3/index.json";

        private const string FeedDotNet6Shipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json";
        private const string FeedDotNet6Transport = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6-transport/nuget/v3/index.json";

        private const string FeedDotNet7Shipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet7/nuget/v3/index.json";
        private const string FeedDotNet7Transport = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet7-transport/nuget/v3/index.json";

        private const string FeedDotNet7InternalShipping = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet7-internal/nuget/v3/index.json";
        private const string FeedDotNet7InternalTransport = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet7-internal-transport/nuget/v3/index.json";

        private const string FeedDotNet6InternalShipping = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal/nuget/v3/index.json";
        private const string FeedDotNet6InternalTransport = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal-transport/nuget/v3/index.json";

        private const string FeedDotNet5InternalShipping = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet5-internal/nuget/v3/index.json";
        private const string FeedDotNet5InternalTransport = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet5-internal-transport/nuget/v3/index.json";

        private const string FeedDotNetLibrariesShipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-libraries/nuget/v3/index.json";
        private const string FeedDotNetLibrariesTransport = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-libraries-transport/nuget/v3/index.json";

        private const string FeedGeneralTestingInternal = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/general-testing-internal/nuget/v3/index.json";

        private const SymbolTargetType InternalSymbolTargets = SymbolTargetType.SymWeb;
        private const SymbolTargetType PublicAndInternalSymbolTargets = SymbolTargetType.Msdl | SymbolTargetType.SymWeb;

        private static List<string> FilenamesToExclude = new List<string>() { 
            "MergedManifest.xml"
        };

        private static TargetFeedSpecification[] DotNet31Feeds =
        {
            (TargetFeedContentType.Package, FeedDotNet31Shipping, AssetSelection.ShippingOnly),
            (TargetFeedContentType.Package, FeedDotNet31Transport, AssetSelection.NonShippingOnly),
            (InstallersAndSymbols, FeedForInstallers),
            (TargetFeedContentType.Checksum, FeedForChecksums),
        };

        private static TargetFeedSpecification[] DotNet31InternalFeeds =
        {
            (TargetFeedContentType.Package, FeedDotNet31InternalShipping, AssetSelection.ShippingOnly),
            (TargetFeedContentType.Package, FeedDotNet31InternalTransport, AssetSelection.NonShippingOnly),
            (InstallersAndSymbols, FeedInternalForInstallers),
            (TargetFeedContentType.Checksum, FeedInternalForChecksums),
        };

        private static TargetFeedSpecification[] DotNet31BlazorFeeds =
        {
            (TargetFeedContentType.Package, FeedDotNet31Blazor, AssetSelection.ShippingOnly),
            (TargetFeedContentType.Package, FeedDotNet31Blazor, AssetSelection.NonShippingOnly),
            (InstallersAndSymbols, FeedInternalForInstallers),
            (TargetFeedContentType.Checksum, FeedInternalForChecksums),
        };

        private static TargetFeedSpecification[] DotNet5Feeds =
        {
            (TargetFeedContentType.Package, FeedDotNet5Shipping, AssetSelection.ShippingOnly),
            (TargetFeedContentType.Package, FeedDotNet5Transport, AssetSelection.NonShippingOnly),
            (InstallersAndSymbols, FeedForInstallers),
            (TargetFeedContentType.Checksum, FeedForChecksums),
        };

        private static TargetFeedSpecification[] DotNet5InternalFeeds =
        {
            (TargetFeedContentType.Package, FeedDotNet5InternalShipping, AssetSelection.ShippingOnly),
            (TargetFeedContentType.Package, FeedDotNet5InternalTransport, AssetSelection.NonShippingOnly),
            (InstallersAndSymbols, FeedInternalForInstallers),
            (TargetFeedContentType.Checksum, FeedInternalForChecksums),
        };

        private static TargetFeedSpecification[] DotNet6Feeds =
        {
            (TargetFeedContentType.Package, FeedDotNet6Shipping, AssetSelection.ShippingOnly),
            (TargetFeedContentType.Package, FeedDotNet6Transport, AssetSelection.NonShippingOnly),
            (InstallersAndSymbols, FeedStagingForInstallers),
            (TargetFeedContentType.Checksum, FeedStagingForChecksums),
        };

        private static TargetFeedSpecification[] DotNet6InternalFeeds =
        {
            (TargetFeedContentType.Package, FeedDotNet6InternalShipping, AssetSelection.ShippingOnly),
            (TargetFeedContentType.Package, FeedDotNet6InternalTransport, AssetSelection.NonShippingOnly),
            (InstallersAndSymbols, FeedStagingInternalForInstallers),
            (TargetFeedContentType.Checksum, FeedStagingInternalForChecksums),
        };

        private static TargetFeedSpecification[] DotNet7Feeds =
        {
            (TargetFeedContentType.Package, FeedDotNet7Shipping, AssetSelection.ShippingOnly),
            (TargetFeedContentType.Package, FeedDotNet7Transport, AssetSelection.NonShippingOnly),
            (InstallersAndSymbols, FeedStagingForInstallers),
            (TargetFeedContentType.Checksum, FeedStagingForChecksums),
        };

        private static TargetFeedSpecification[] DotNet7InternalFeeds =
        {
            (TargetFeedContentType.Package, FeedDotNet7InternalShipping, AssetSelection.ShippingOnly),
            (TargetFeedContentType.Package, FeedDotNet7InternalTransport, AssetSelection.NonShippingOnly),
            (InstallersAndSymbols, FeedStagingInternalForInstallers),
            (TargetFeedContentType.Checksum, FeedStagingInternalForChecksums),
        };

        private static TargetFeedSpecification[] DotNetEngFeeds =
        {
            (TargetFeedContentType.Package, FeedDotNetEng, AssetSelection.ShippingOnly),
            (TargetFeedContentType.Package, FeedDotNetEng, AssetSelection.NonShippingOnly),
            (InstallersAndSymbols, FeedStagingForInstallers),
            (TargetFeedContentType.Checksum, FeedStagingForChecksums),
        };

        private static TargetFeedSpecification[] DotNetToolsFeeds =
        {
            (TargetFeedContentType.Package, FeedDotNetTools, AssetSelection.ShippingOnly),
            (TargetFeedContentType.Package, FeedDotNetTools, AssetSelection.NonShippingOnly),
            (InstallersAndSymbols, FeedStagingForInstallers),
            (TargetFeedContentType.Checksum, FeedStagingForChecksums),
        };

        private static TargetFeedSpecification[] DotNetToolsInternalFeeds =
        {
            (TargetFeedContentType.Package, FeedDotNetToolsInternal, AssetSelection.ShippingOnly),
            (TargetFeedContentType.Package, FeedDotNetToolsInternal, AssetSelection.NonShippingOnly),
            (InstallersAndSymbols, FeedStagingInternalForInstallers),
            (TargetFeedContentType.Checksum, FeedStagingInternalForChecksums),
        };

        private static TargetFeedSpecification[] DotNetExperimentalFeeds =
        {
            (TargetFeedContentType.Package, FeedDotNetExperimental, AssetSelection.ShippingOnly),
            (TargetFeedContentType.Package, FeedDotNetExperimental, AssetSelection.NonShippingOnly),
            (InstallersAndSymbols, FeedStagingForInstallers),
            (TargetFeedContentType.Checksum, FeedStagingForChecksums),
        };

        private static TargetFeedSpecification[] GeneralTestingFeeds =
        {
            (TargetFeedContentType.Package, FeedGeneralTesting, AssetSelection.ShippingOnly),
            (TargetFeedContentType.Package, FeedGeneralTesting, AssetSelection.NonShippingOnly),
            (InstallersAndSymbols, FeedStagingForInstallers),
            (TargetFeedContentType.Checksum, FeedStagingForChecksums),
        };

        private static TargetFeedSpecification[] GeneralTestingInternalFeeds =
        {
            (TargetFeedContentType.Package, FeedGeneralTestingInternal, AssetSelection.ShippingOnly),
            (TargetFeedContentType.Package, FeedGeneralTestingInternal, AssetSelection.NonShippingOnly),
            (InstallersAndSymbols, FeedStagingInternalForInstallers),
            (TargetFeedContentType.Checksum, FeedStagingInternalForChecksums),
        };
        #endregion

        #region Target Channel Configs
        public static readonly List<TargetChannelConfig> ChannelInfos = new List<TargetChannelConfig>() {
            // ".NET 5 Dev",
            new TargetChannelConfig(
                131,
                false,
                PublishingInfraVersion.All,
                new List<string>() { "5.0" },
                DotNet5Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 7",
            new TargetChannelConfig(
                2236,
                false,
                PublishingInfraVersion.All,
                new List<string>() { "7.0" },
                DotNet7Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 7 SDK 7.0.1xx",
            new TargetChannelConfig(
                2237,
                false,
                PublishingInfraVersion.All,
                new List<string>() { "7.0.1xx", "7.0" },
                DotNet7Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 7 Internal",
            new TargetChannelConfig(
                3035,
                true,
                PublishingInfraVersion.All,
                new List<string>() { "internal/7.0" },
                DotNet7InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 7 SDK 7.0.1xx Internal",
            new TargetChannelConfig(
                3038,
                true,
                PublishingInfraVersion.All,
                new List<string>() { "internal/7.0.1xx", "internal/7.0" },
                DotNet7InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 7 Preview 7",
            new TargetChannelConfig(
                2843,
                false,
                PublishingInfraVersion.All,
                new List<string>() { "7.0-preview7" },
                DotNet7Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 7 SDK 7.0.1xx Preview 7",
            new TargetChannelConfig(
                2840,
                false,
                PublishingInfraVersion.All,
                new List<string>() { "7.0.1xx-preview7", "7.0-preview7" },
                DotNet7Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 7 RC 1 Internal",
            new TargetChannelConfig(
                3033,
                true,
                PublishingInfraVersion.All,
                new List<string>() { "internal/7.0-rc1" },
                DotNet7InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 7 SDK 7.0.1xx RC 1 Internal",
            new TargetChannelConfig(
                3036,
                true,
                PublishingInfraVersion.All,
                new List<string>() { "internal/7.0.1xx-rc1", "internal/7.0-rc1" },
                DotNet7InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 7 RC 2 Internal",
            new TargetChannelConfig(
                3034,
                true,
                PublishingInfraVersion.All,
                new List<string>() { "internal/7.0-rc2" },
                DotNet7InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 7 SDK 7.0.1xx RC 2 Internal",
            new TargetChannelConfig(
                3037,
                true,
                PublishingInfraVersion.All,
                new List<string>() { "internal/7.0.1xx-rc2", "internal/7.0-rc2" },
                DotNet7InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6",
            new TargetChannelConfig(
                1296,
                false,
                PublishingInfraVersion.All,
                new List<string>() { "6.0" },
                DotNet6Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6 Internal",
            new TargetChannelConfig(
                2097,
                true,
                PublishingInfraVersion.All,
                new List<string>() { "internal/6.0" },
                DotNet6InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6 Private",
            new TargetChannelConfig(
                2693,
                true,
                PublishingInfraVersion.All,
                new List<string>() { "internal/6.0-private" },
                DotNet6InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6 SDK 6.0.Nxx Private",
            new TargetChannelConfig(
                2695,
                true,
                PublishingInfraVersion.All,
                new List<string>() { "internal/6.0.Nxx-private" },
                DotNet6InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),
                
            // ".NET 6 MAUI",
            new TargetChannelConfig(
                2453,
                false,
                PublishingInfraVersion.All,
                new List<string>() { "maui/6.0" },
                DotNet6Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6 SDK 6.0.1xx",
            new TargetChannelConfig(
                1792,
                false,
                PublishingInfraVersion.All,
                new List<string>() { "6.0.1xx" },
                DotNet6Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6 SDK 6.0.1xx Internal",
            new TargetChannelConfig(
                2098,
                true,
                PublishingInfraVersion.All,
                new List<string>() { "internal/6.0.1xx" },
                DotNet6InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),
            
            // ".NET 6 SDK 6.0.2xx",
            new TargetChannelConfig(
                2434,
                false,
                PublishingInfraVersion.All,
                new List<string>() { "6.0.2xx" },
                DotNet6Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6 SDK 6.0.2xx Internal",
            new TargetChannelConfig(
                2435,
                true,
                PublishingInfraVersion.All,
                new List<string>() { "internal/6.0.2xx" },
                DotNet6InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6 SDK 6.0.3xx",
            new TargetChannelConfig(
                2551,
                false,
                PublishingInfraVersion.All,
                new List<string>() { "6.0.3xx" },
                DotNet6Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6 SDK 6.0.3xx Internal",
            new TargetChannelConfig(
                2552,
                true,
                PublishingInfraVersion.All,
                new List<string>() { "internal/6.0.3xx"},
                DotNet6InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6 SDK 6.0.4xx",
            new TargetChannelConfig(
                2696,
                false,
                PublishingInfraVersion.All,
                new List<string>() { "6.0.4xx", "6.0" },
                DotNet6Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6 SDK 6.0.4xx Internal",
            new TargetChannelConfig(
                2697,
                true,
                PublishingInfraVersion.All,
                new List<string>() { "internal/6.0.4xx", "internal/6.0" },
                DotNet6InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5" (public),
            new TargetChannelConfig(
                1299,
                false,
                PublishingInfraVersion.Next,
                akaMSChannelNames: new List<string>() { "5.0" },
                DotNet5Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 Internal" (internal),
            new TargetChannelConfig(
                1300,
                true,
                PublishingInfraVersion.Next,
                akaMSChannelNames: new List<string>() { "internal/5.0" },
                DotNet5InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 SDK 5.0.1xx" (public),
            new TargetChannelConfig(
                1297,
                false,
                PublishingInfraVersion.Next,
                akaMSChannelNames: new List<string>() { "5.0.1xx" },
                DotNet5Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 SDK 5.0.1xx Internal" (internal),
            new TargetChannelConfig(
                1298,
                true,
                PublishingInfraVersion.Next,
                akaMSChannelNames: new List<string>() { "internal/5.0.1xx" },
                DotNet5InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 SDK 5.0.2xx" (public),
            new TargetChannelConfig(
                1518,
                false,
                PublishingInfraVersion.Next,
                akaMSChannelNames: new List<string>() { "5.0.2xx" },
                DotNet5Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 SDK 5.0.2xx Internal" (internal),
            new TargetChannelConfig(
                1519,
                true,
                PublishingInfraVersion.Next,
                akaMSChannelNames: new List<string>() { "internal/5.0.2xx" },
                DotNet5InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 SDK 5.0.3xx" (public),
            new TargetChannelConfig(
                1754,
                false,
                PublishingInfraVersion.Next,
                akaMSChannelNames: new List<string>() { "5.0.3xx" },
                DotNet5Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 SDK 5.0.3xx Internal" (internal),
            new TargetChannelConfig(
                1755,
                true,
                PublishingInfraVersion.Next,
                akaMSChannelNames: new List<string>() { "internal/5.0.3xx" },
                DotNet5InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 SDK 5.0.4xx" (public),
            new TargetChannelConfig(
                1985,
                false,
                PublishingInfraVersion.Next,
                akaMSChannelNames: new List<string>() { "5.0.4xx", "5.0" },
                DotNet5Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 SDK 5.0.4xx Internal" (internal),
            new TargetChannelConfig(
                1986,
                true,
                PublishingInfraVersion.Next,
                akaMSChannelNames: new List<string>() { "internal/5.0.4xx" },
                DotNet5InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),
                
            // ".NET 5 SDK 5.0.5xx Internal" (internal),
            new TargetChannelConfig(
                2788,
                true,
                PublishingInfraVersion.Next,
                akaMSChannelNames: new List<string>() { "internal/5.0.5xx" },
                DotNet5InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Eng - Latest",
            new TargetChannelConfig(
                2,
                false,
                PublishingInfraVersion.All,
                new List<string>() { "eng" },
                DotNetEngFeeds,
                PublicAndInternalSymbolTargets,
                flatten: false),

            // ".NET 5 Eng",
            new TargetChannelConfig(
                1495,
                false,
                PublishingInfraVersion.Next,
                new List<string>() { "eng/net5" },
                DotNetEngFeeds,
                PublicAndInternalSymbolTargets,
                flatten: false),

            // ".NET 6 Eng",
            new TargetChannelConfig(
                2293,
                false,
                PublishingInfraVersion.Next,
                new List<string>() { "eng/net6" },
                DotNetEngFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Eng - Validation",
            new TargetChannelConfig(
                9,
                false,
                PublishingInfraVersion.All,
                new List<string>() { "eng/validation" },
                DotNetEngFeeds,
                PublicAndInternalSymbolTargets,
                flatten: false),

            // ".NET 5 Eng - Validation",
            new TargetChannelConfig(
                1496,
                false,
                PublishingInfraVersion.Next,
                new List<string>() { "eng/net5validation" },
                DotNetEngFeeds,
                PublicAndInternalSymbolTargets,
                flatten: false),

            // ".NET 6 Eng - Validation",
            new TargetChannelConfig(
                2294,
                false,
                PublishingInfraVersion.Next,
                new List<string>() { "eng/net6validation" },
                DotNetEngFeeds,
                PublicAndInternalSymbolTargets,
                flatten: false),

            // "General Testing",
            new TargetChannelConfig(
                529,
                false,
                PublishingInfraVersion.All,
                new List<string>() { "generaltesting" },
                GeneralTestingFeeds,
                PublicAndInternalSymbolTargets),

            // "General Testing Internal",
            new TargetChannelConfig(
                1647,
                true,
                PublishingInfraVersion.All,
                new List<string>() { "generaltestinginternal" },
                GeneralTestingInternalFeeds,
                InternalSymbolTargets),

            // ".NET Core Tooling Dev",
            new TargetChannelConfig(
                548,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>(),
                DotNetToolsFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Core Tooling Release",
            new TargetChannelConfig(
                549,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>(),
                DotNetToolsFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Internal Tooling",
            new TargetChannelConfig(
                551,
                true,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>(),
                DotNetToolsInternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Core Experimental",
            new TargetChannelConfig(
                562,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>(),
                DotNetExperimentalFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Eng Services - Int",
            new TargetChannelConfig(
                678,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>(),
                DotNetEngFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Eng Services - Prod",
            new TargetChannelConfig(
                679,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>(),
                DotNetEngFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET 3 Tools",
            new TargetChannelConfig(
                344,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>(),
                DotNetEngFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET 3 Tools - Validation",
            new TargetChannelConfig(
                390,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>(),
                DotNetEngFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Core Tooling Dev",
            new TargetChannelConfig(
                548,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>(),
                DotNetToolsFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Core Tooling Release",
            new TargetChannelConfig(
                549,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>(),
                DotNetToolsFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Core 3.1 Dev",
            new TargetChannelConfig(
                128,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>() { "3.1" },
                DotNet31Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core 3.1 Release",
            new TargetChannelConfig(
                129,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>() { "3.1" },
                DotNet31Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core SDK 3.1.2xx",
            new TargetChannelConfig(
                558,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>() { "3.1.2xx" },
                DotNet31Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // "NET Core SDK 3.1.1xx",
            new TargetChannelConfig(
                560,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>() { "3.1.1xx" },
                DotNet31Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core SDK 3.1.3xx",
            new TargetChannelConfig(
                759,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>() { "3.1.3xx" },
                DotNet31Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core SDK 3.1.4xx",
            new TargetChannelConfig(
                921,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>() { "3.1.4xx" },
                DotNet31Feeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core SDK 3.1.3xx Internal",
            new TargetChannelConfig(
                760,
                true,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>() { "internal/3.1.3xx" },
                DotNet31InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core 3.1 Internal Servicing",
            new TargetChannelConfig(
                550,
                true,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>() { "internal/3.1" },
                DotNet31InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core SDK 3.1.2xx Internal",
            new TargetChannelConfig(
                557,
                true,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>() { "internal/3.1.2xx" },
                DotNet31InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core SDK 3.1.1xx Internal",
            new TargetChannelConfig(
                559,
                true,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>() { "internal/3.1.1xx" },
                DotNet31InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core SDK 3.1.4xx Internal",
            new TargetChannelConfig(
                922,
                true,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>() { "internal/3.1.4xx" },
                DotNet31InternalFeeds,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core 3.1 Blazor Features",
            new TargetChannelConfig(
                531,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>(),
                DotNet31BlazorFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // "VS 16.6",
            new TargetChannelConfig(
                1010,
                false,
                PublishingInfraVersion.All,
                new List<string>(),
                DotNetToolsFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // "VS 16.7",
            new TargetChannelConfig(
                1011,
                false,
                PublishingInfraVersion.All,
                new List<string>(),
                DotNetToolsFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // "VS 16.8",
            new TargetChannelConfig(
                1154,
                false,
                PublishingInfraVersion.All,
                new List<string>(),
                DotNetToolsFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // "VS 16.9",
            new TargetChannelConfig(
                1473,
                false,
                PublishingInfraVersion.All,
                new List<string>(),
                DotNetToolsFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // "VS 16.10",
            new TargetChannelConfig(
                1692,
                false,
                PublishingInfraVersion.All,
                new List<string>(),
                DotNetToolsFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // "VS 16.11",
            new TargetChannelConfig(
                1926,
                false,
                PublishingInfraVersion.All,
                new List<string>(),
                DotNetToolsFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // "VS 17.0",
            new TargetChannelConfig(
                1853,
                false,
                PublishingInfraVersion.All,
                new List<string>(),
                DotNetToolsFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // "VS 17.1",
            new TargetChannelConfig(
                2346,
                false,
                PublishingInfraVersion.All,
                new List<string>(),
                DotNetToolsFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // "VS 17.2",
            new TargetChannelConfig(
                2542,
                false,
                PublishingInfraVersion.All,
                new List<string>(),
                DotNetToolsFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),
            
            // "VS 17.3",
            new TargetChannelConfig(
                2692,
                false,
                PublishingInfraVersion.All,
                new List<string>(),
                DotNetToolsFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),
            
            // "VS 17.4",
            new TargetChannelConfig(
                2914,
                false,
                PublishingInfraVersion.All,
                new List<string>(),
                DotNetToolsFeeds,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Libraries",
            new TargetChannelConfig(
                1648,
                false,
                PublishingInfraVersion.All,
                akaMSChannelNames: new List<string>(),
                new TargetFeedSpecification[]
                {
                    (TargetFeedContentType.Package, FeedDotNetLibrariesShipping, AssetSelection.ShippingOnly),
                    (TargetFeedContentType.Package, FeedDotNetLibrariesTransport, AssetSelection.NonShippingOnly),
                    (InstallersAndSymbols, FeedForInstallers),
                    (TargetFeedContentType.Checksum, FeedForChecksums),
                },
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),
        };
        #endregion
    }
}
