// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Build.Tasks.Feed.Model
{
    /// <summary>
    /// Hold properties of a target feed endpoint.
    /// </summary>
    public class TargetFeedConfig
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

    /// <summary>
    /// Whether the target feed URL points to an Azure Feed or an Sleet Feed.
    /// </summary>
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
}
