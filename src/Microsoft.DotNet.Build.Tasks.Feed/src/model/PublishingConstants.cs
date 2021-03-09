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
            @"https://pkgs.dev.azure.com/(?<account>[a-zA-Z0-9]+)/(?<visibility>[a-zA-Z0-9-]+/)?_packaging/(?<feed>.+)/nuget/v3/index.json";

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

        #region Target Channel Configs
        private const string FeedForChecksums = "https://dotnetclichecksums.blob.core.windows.net/dotnet/index.json";
        private const string FeedForInstallers = "https://dotnetcli.blob.core.windows.net/dotnet/index.json";

        private const string FeedInternalForChecksums = "https://dotnetclichecksumsmsrc.blob.core.windows.net/dotnet/index.json";
        private const string FeedInternalForInstallers = "https://dotnetclimsrc.blob.core.windows.net/dotnet/index.json";

        private const string FeedGeneralTesting = "https://pkgs.dev.azure.com/dnceng/public/_packaging/general-testing/nuget/v3/index.json";
        private const string FeedGeneralTestingSymbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/general-testing-symbols/nuget/v3/index.json";

        private const string FeedDotNetExperimental = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-experimental/nuget/v3/index.json";
        private const string FeedDotNetExperimentalSymbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-experimental-symbols/nuget/v3/index.json";

        private const string FeedDotNetEngShipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json";
        private const string FeedDotNetEngTransport = FeedDotNetEngShipping;
        private const string FeedDotNetEngSymbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng-symbols/nuget/v3/index.json";

        private const string FeedDotNetToolsShipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools/nuget/v3/index.json";
        private const string FeedDotNetToolsTransport = FeedDotNetToolsShipping;
        private const string FeedDotNetToolsSymbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-tools-symbols/nuget/v3/index.json";

        private const string FeedDotNetToolsInternalShipping = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet-tools-internal/nuget/v3/index.json";
        private const string FeedDotNetToolsInternalTransport = FeedDotNetToolsInternalShipping;
        private const string FeedDotNetToolsInternalSymbols = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet-tools-internal-symbols/nuget/v3/index.json";

        private const string FeedDotNet31Shipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1/nuget/v3/index.json";
        private const string FeedDotNet31Transport = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-transport/nuget/v3/index.json";
        private const string FeedDotNet31Symbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-symbols/nuget/v3/index.json";

        private const string FeedDotNet31InternalShipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-internal/nuget/v3/index.json";
        private const string FeedDotNet31InternalTransport = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-internal-transport/nuget/v3/index.json";
        private const string FeedDotNet31InternalSymbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-internal-symbols/nuget/v3/index.json";

        private const string FeedDotNet31BlazorShipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-blazor/nuget/v3/index.json";
        private const string FeedDotNet31BlazorTransport = FeedDotNet31BlazorShipping;
        private const string FeedDotNet31BlazorSymbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet3.1-blazor-symbols/nuget/v3/index.json";

        private const string FeedDotNet5Shipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet5/nuget/v3/index.json";
        private const string FeedDotNet5Transport = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet5-transport/nuget/v3/index.json";
        private const string FeedDotNet5Symbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet5-symbols/nuget/v3/index.json";

        private const string FeedDotNet6Shipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json";
        private const string FeedDotNet6Transport = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6-transport/nuget/v3/index.json";
        private const string FeedDotNet6Symbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6-symbols/nuget/v3/index.json";

        private const string FeedDotNet5InternalShipping = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet5-internal/nuget/v3/index.json";
        private const string FeedDotNet5InternalTransport = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet5-internal-transport/nuget/v3/index.json";
        private const string FeedDotNet5InternalSymbols = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet5-internal-symbols/nuget/v3/index.json";

        private const string FeedDotNetLibrariesShipping = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-libraries/nuget/v3/index.json";
        private const string FeedDotNetLibrariesTransport = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-libraries-transport/nuget/v3/index.json";
        private const string FeedDotNetLibrariesSymbols = "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-libraries-symbols/nuget/v3/index.json";

        private const string FeedGeneralTestingInternal = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/general-testing-internal/nuget/v3/index.json";
        private const string FeedGeneralTestingInternalSymbols = "https://pkgs.dev.azure.com/dnceng/internal/_packaging/general-testing-internal/nuget/v3/index.json";

        private const SymbolTargetType InternalSymbolTargets = SymbolTargetType.SymWeb;
        private const SymbolTargetType PublicAndInternalSymbolTargets = SymbolTargetType.Msdl | SymbolTargetType.SymWeb;

        private static List<string> FilenamesToExclude = new List<string>() { 
            "MergedManifest.xml"
        };

        public static readonly List<TargetChannelConfig> ChannelInfos = new List<TargetChannelConfig>() {
            // ".NET 5 Dev",
            new TargetChannelConfig(
                131,
                false,
                PublishingInfraVersion.All,
                "5.0",
                FeedDotNet5Shipping,
                FeedDotNet5Transport,
                FeedDotNet5Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6",
            new TargetChannelConfig(
                1296,
                false,
                PublishingInfraVersion.All,
                "6.0",
                FeedDotNet6Shipping,
                FeedDotNet6Transport,
                FeedDotNet6Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6 SDK 6.0.1xx",
            new TargetChannelConfig(
                1792,
                false,
                PublishingInfraVersion.All,
                "6.0.1xx",
                FeedDotNet6Shipping,
                FeedDotNet6Transport,
                FeedDotNet6Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6 Preview 1",
            new TargetChannelConfig(
                1670,
                false,
                PublishingInfraVersion.All,
                "6.0-preview1",
                FeedDotNet6Shipping,
                FeedDotNet6Transport,
                FeedDotNet6Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6 Preview 2" ,
            new TargetChannelConfig(
                1752,
                false,
                PublishingInfraVersion.All,
                "6.0-preview2",
                FeedDotNet6Shipping,
                FeedDotNet6Transport,
                FeedDotNet6Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 6.0.1xx SDK Preview 2",
            new TargetChannelConfig(
                1753,
                false,
                PublishingInfraVersion.All,
                "6.0.1xx-preview2",
                FeedDotNet6Shipping,
                FeedDotNet6Transport,
                FeedDotNet6Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5" (public),
            new TargetChannelConfig(
                1299,
                false,
                PublishingInfraVersion.Next,
                akaMSChannelName: "5.0",
                FeedDotNet5Shipping,
                FeedDotNet5Transport,
                FeedDotNet5Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 Internal" (internal),
            new TargetChannelConfig(
                1300,
                true,
                PublishingInfraVersion.Next,
                akaMSChannelName: "internal/5.0",
                FeedDotNet5InternalShipping,
                FeedDotNet5InternalTransport,
                FeedDotNet5InternalSymbols,
                FeedInternalForChecksums,
                FeedInternalForInstallers,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 SDK 5.0.1xx" (public),
            new TargetChannelConfig(
                1297,
                false,
                PublishingInfraVersion.Next,
                akaMSChannelName: "5.0.1xx",
                FeedDotNet5Shipping,
                FeedDotNet5Transport,
                FeedDotNet5Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 SDK 5.0.1xx Internal" (internal),
            new TargetChannelConfig(
                1298,
                true,
                PublishingInfraVersion.Next,
                akaMSChannelName: "internal/5.0.1xx",
                FeedDotNet5InternalShipping,
                FeedDotNet5InternalTransport,
                FeedDotNet5InternalSymbols,
                FeedInternalForChecksums,
                FeedInternalForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 SDK 5.0.2xx" (public),
            new TargetChannelConfig(
                1518,
                false,
                PublishingInfraVersion.Next,
                akaMSChannelName: "5.0.2xx",
                FeedDotNet5Shipping,
                FeedDotNet5Transport,
                FeedDotNet5Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 SDK 5.0.2xx Internal" (internal),
            new TargetChannelConfig(
                1519,
                true,
                PublishingInfraVersion.Next,
                akaMSChannelName: "internal/5.0.2xx",
                FeedDotNet5InternalShipping,
                FeedDotNet5InternalTransport,
                FeedDotNet5InternalSymbols,
                FeedInternalForChecksums,
                FeedInternalForInstallers,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 SDK 5.0.3xx" (public),
            new TargetChannelConfig(
                1754,
                false,
                PublishingInfraVersion.Next,
                akaMSChannelName: "5.0.3xx",
                FeedDotNet5Shipping,
                FeedDotNet5Transport,
                FeedDotNet5Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET 5 SDK 5.0.3xx Internal" (internal),
            new TargetChannelConfig(
                1755,
                true,
                PublishingInfraVersion.Next,
                akaMSChannelName: "internal/5.0.3xx",
                FeedDotNet5InternalShipping,
                FeedDotNet5InternalTransport,
                FeedDotNet5InternalSymbols,
                FeedInternalForChecksums,
                FeedInternalForInstallers,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Eng - Latest",
            new TargetChannelConfig(
                2,
                false,
                PublishingInfraVersion.All,
                "eng",
                FeedDotNetEngShipping,
                FeedDotNetEngTransport,
                FeedDotNetEngSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                flatten: false),

            // ".NET 5 Eng",
            new TargetChannelConfig(
                1495,
                false,
                PublishingInfraVersion.Next,
                "eng/net5",
                FeedDotNetEngShipping,
                FeedDotNetEngTransport,
                FeedDotNetEngSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                flatten: false),

            // ".NET Eng - Validation",
            new TargetChannelConfig(
                9,
                false,
                PublishingInfraVersion.All,
                "eng/validation",
                FeedDotNetEngShipping,
                FeedDotNetEngTransport,
                FeedDotNetEngSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                flatten: false),

            // ".NET 5 Eng - Validation",
            new TargetChannelConfig(
                1496,
                false,
                PublishingInfraVersion.Next,
                "eng/net5validation",
                FeedDotNetEngShipping,
                FeedDotNetEngTransport,
                FeedDotNetEngSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                flatten: false),

            // "General Testing",
            new TargetChannelConfig(
                529,
                false,
                PublishingInfraVersion.All,
                "generaltesting",
                FeedGeneralTesting,
                FeedGeneralTesting,
                FeedGeneralTestingSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets),

            // "General Testing Internal",
            new TargetChannelConfig(
                1647,
                true,
                PublishingInfraVersion.All,
                "generaltestinginternal",
                FeedGeneralTestingInternal,
                FeedGeneralTestingInternal,
                FeedGeneralTestingInternalSymbols,
                FeedInternalForChecksums,
                FeedInternalForInstallers,
                InternalSymbolTargets),

            // ".NET Core Tooling Dev",
            new TargetChannelConfig(
                548,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: string.Empty,
                FeedDotNetToolsShipping,
                FeedDotNetToolsTransport,
                FeedDotNetToolsSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Core Tooling Release",
            new TargetChannelConfig(
                549,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: string.Empty,
                FeedDotNetToolsShipping,
                FeedDotNetToolsTransport,
                FeedDotNetToolsSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Internal Tooling",
            new TargetChannelConfig(
                551,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: string.Empty,
                FeedDotNetToolsInternalShipping,
                FeedDotNetToolsInternalTransport,
                FeedDotNetToolsInternalSymbols,
                FeedInternalForChecksums,
                FeedInternalForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Core Experimental",
            new TargetChannelConfig(
                562,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: string.Empty,
                FeedDotNetExperimental,
                FeedDotNetExperimental,
                FeedDotNetExperimentalSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Eng Services - Int",
            new TargetChannelConfig(
                678,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: string.Empty,
                FeedDotNetEngShipping,
                FeedDotNetEngTransport,
                FeedDotNetEngSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Eng Services - Prod",
            new TargetChannelConfig(
                679,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: string.Empty,
                FeedDotNetEngShipping,
                FeedDotNetEngTransport,
                FeedDotNetEngSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET 3 Tools",
            new TargetChannelConfig(
                344,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: string.Empty,
                FeedDotNetEngShipping,
                FeedDotNetEngTransport,
                FeedDotNetEngSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET 3 Tools - Validation",
            new TargetChannelConfig(
                390,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: string.Empty,
                FeedDotNetEngShipping,
                FeedDotNetEngTransport,
                FeedDotNetEngSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Core Tooling Dev",
            new TargetChannelConfig(
                548,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: string.Empty,
                FeedDotNetToolsShipping,
                FeedDotNetToolsTransport,
                FeedDotNetToolsSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Core Tooling Release",
            new TargetChannelConfig(
                549,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: string.Empty,
                FeedDotNetToolsShipping,
                FeedDotNetToolsTransport,
                FeedDotNetToolsSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Core 3.1 Dev",
            new TargetChannelConfig(
                128,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: "3.1",
                FeedDotNet31Shipping,
                FeedDotNet31Transport,
                FeedDotNet31Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core 3.1 Release",
            new TargetChannelConfig(
                129,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: "3.1",
                FeedDotNet31Shipping,
                FeedDotNet31Transport,
                FeedDotNet31Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core SDK 3.1.2xx",
            new TargetChannelConfig(
                558,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: "3.1.2xx",
                FeedDotNet31Shipping,
                FeedDotNet31Transport,
                FeedDotNet31Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // "NET Core SDK 3.1.1xx",
            new TargetChannelConfig(
                560,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: "3.1.1xx",
                FeedDotNet31Shipping,
                FeedDotNet31Transport,
                FeedDotNet31Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core SDK 3.1.3xx",
            new TargetChannelConfig(
                759,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: "3.1.3xx",
                FeedDotNet31Shipping,
                FeedDotNet31Transport,
                FeedDotNet31Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core SDK 3.1.4xx",
            new TargetChannelConfig(
                921,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: "3.1.4xx",
                FeedDotNet31Shipping,
                FeedDotNet31Transport,
                FeedDotNet31Symbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core SDK 3.1.3xx Internal",
            new TargetChannelConfig(
                760,
                true,
                PublishingInfraVersion.All,
                akaMSChannelName: "internal/3.1.3xx",
                FeedDotNet31InternalShipping,
                FeedDotNet31InternalTransport,
                FeedDotNet31InternalSymbols,
                FeedForChecksums,
                FeedForInstallers,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core 3.1 Internal Servicing",
            new TargetChannelConfig(
                550,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: "internal/3.1",
                FeedDotNet31InternalShipping,
                FeedDotNet31InternalTransport,
                FeedDotNet31InternalSymbols,
                FeedForChecksums,
                FeedForInstallers,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core SDK 3.1.2xx Internal",
            new TargetChannelConfig(
                557,
                true,
                PublishingInfraVersion.All,
                akaMSChannelName: "internal/3.1.2xx",
                FeedDotNet31InternalShipping,
                FeedDotNet31InternalTransport,
                FeedDotNet31InternalSymbols,
                FeedForChecksums,
                FeedForInstallers,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core SDK 3.1.1xx Internal",
            new TargetChannelConfig(
                559,
                true,
                PublishingInfraVersion.All,
                akaMSChannelName: "internal/3.1.1xx",
                FeedDotNet31InternalShipping,
                FeedDotNet31InternalTransport,
                FeedDotNet31InternalSymbols,
                FeedForChecksums,
                FeedForInstallers,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core SDK 3.1.4xx Internal",
            new TargetChannelConfig(
                922,
                true,
                PublishingInfraVersion.All,
                akaMSChannelName: "internal/3.1.4xx",
                FeedDotNet31InternalShipping,
                FeedDotNet31InternalTransport,
                FeedDotNet31InternalSymbols,
                FeedForChecksums,
                FeedForInstallers,
                InternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // ".NET Core 3.1 Blazor Features",
            new TargetChannelConfig(
                531,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: string.Empty,
                FeedDotNet31BlazorShipping,
                FeedDotNet31BlazorTransport,
                FeedDotNet31BlazorSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude),

            // "VS 16.6",
            new TargetChannelConfig(
                1010,
                false,
                PublishingInfraVersion.All,
                string.Empty,
                FeedDotNetToolsShipping,
                FeedDotNetToolsTransport,
                FeedDotNetToolsSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // "VS 16.7",
            new TargetChannelConfig(
                1011,
                false,
                PublishingInfraVersion.All,
                string.Empty,
                FeedDotNetToolsShipping,
                FeedDotNetToolsTransport,
                FeedDotNetToolsSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // "VS 16.8",
            new TargetChannelConfig(
                1154,
                false,
                PublishingInfraVersion.All,
                string.Empty,
                FeedDotNetToolsShipping,
                FeedDotNetToolsTransport,
                FeedDotNetToolsSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // "VS 16.9",
            new TargetChannelConfig(
                1473,
                false,
                PublishingInfraVersion.All,
                string.Empty,
                FeedDotNetToolsShipping,
                FeedDotNetToolsTransport,
                FeedDotNetToolsSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // "VS 16.10",
            new TargetChannelConfig(
                1692,
                false,
                PublishingInfraVersion.All,
                string.Empty,
                FeedDotNetToolsShipping,
                FeedDotNetToolsTransport,
                FeedDotNetToolsSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // "VS Master",
            new TargetChannelConfig(
                1012,
                false,
                PublishingInfraVersion.All,
                string.Empty,
                FeedDotNetToolsShipping,
                FeedDotNetToolsTransport,
                FeedDotNetToolsSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),

            // ".NET Libraries",
            new TargetChannelConfig(
                1648,
                false,
                PublishingInfraVersion.All,
                akaMSChannelName: string.Empty,
                FeedDotNetLibrariesShipping,
                FeedDotNetLibrariesTransport,
                FeedDotNetLibrariesSymbols,
                FeedForChecksums,
                FeedForInstallers,
                PublicAndInternalSymbolTargets,
                filenamesToExclude: FilenamesToExclude,
                flatten: false),
        };
        #endregion
    }
}
