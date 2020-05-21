// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.Build.Tasks.Feed.Model
{
    /// <summary>
    /// Hold properties of a target feed endpoint.
    /// </summary>
    public class TargetFeedConfig
    {
        public TargetFeedContentType ContentType { get; set; }
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
        public string LatestLinkShortUrlPrefix { get; set; } = string.Empty;

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            TargetFeedConfig other = (TargetFeedConfig)obj;

            var result =  
                TargetURL.Equals(other.TargetURL, StringComparison.OrdinalIgnoreCase) &&
                Token.Equals(other.Token) &&
                LatestLinkShortUrlPrefix.Equals(other.LatestLinkShortUrlPrefix, StringComparison.OrdinalIgnoreCase) &&
                (ContentType == other.ContentType) &&
                (Type == other.Type) &&
                (AssetSelection == other.AssetSelection) &&
                (Isolated == other.Isolated) &&
                (Internal == other.Internal) &&
                (AllowOverwrite == other.AllowOverwrite);

            if (result == false)
            {
                return false;
            }

            return true;
        }

        public override int GetHashCode()
        {
            return (ContentType, Type, AssetSelection, Isolated, Internal, AllowOverwrite, LatestLinkShortUrlPrefix, TargetURL,  Token).GetHashCode();
        }

        public override string ToString()
        {
            return 
                $"\n Content-type: '{ContentType}' " +
                $"\n Feed-type: '{Type}' " +
                $"\n AssetSelection: '{AssetSelection}' " +
                $"\n Isolated? '{Isolated}' " +
                $"\n Internal? '{Internal}' " +
                $"\n AllowOverwrite? '{AllowOverwrite}' " +
                $"\n ShortUrlPrefix: '{LatestLinkShortUrlPrefix}' " +
                $"\n TargetURL: '{TargetURL}'";
        }
    }

    [Flags]
    public enum TargetFeedContentType
    {
        Package         = 1,
        Symbols         = 2,
        Checksum        = 4,
        OSX             = 8,
        Deb             = 16,
        Rpm             = 32,
        Node            = 64,
        BinaryLayout    = 128,
        Installer       = 256,
        Maven           = 512,
        VSIX            = 1024,
        Badge           = 2048,
        Other           = 4096
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
