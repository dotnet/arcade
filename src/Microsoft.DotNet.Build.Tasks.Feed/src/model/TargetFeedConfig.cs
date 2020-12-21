// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Azure;

namespace Microsoft.DotNet.Build.Tasks.Feed.Model
{
    /// <summary>
    /// Hold properties of a target feed endpoint.
    /// </summary>
    public class TargetFeedConfig
    {
        public TargetFeedContentType ContentType { get; }

        public string TargetURL { get; }

        public FeedType Type { get; }

        public string Token { get; }

        public AssetSelection AssetSelection { get; }
        
        /// <summary>
        /// If true, the feed is treated as 'isolated', meaning nuget packages pushed
        /// to it may be stable.
        /// </summary>
        public bool Isolated { get; }
        
        /// <summary>
        /// If true, the feed is treated as 'internal', meaning artifacts from an internal build
        /// can be published here.
        /// </summary>
        public bool Internal { get; }
        
        /// <summary>
        /// If true, the items on the feed can be overwritten. This is only
        /// valid for azure blob storage feeds.
        /// </summary>
        public bool AllowOverwrite { get; }
        
        /// <summary>
        /// Prefix of aka.ms links that should be generated for blobs.
        /// Not applicable to packages
        /// Generates a link the blob, stripping away any version information in the file or blob path.
        /// E.g. 
        ///      [LatestLinkShortUrlPrefix]/aspnetcore/Runtime/dotnet-hosting-win.exe -> aspnetcore/Runtime/3.1.0-preview2.19511.6/dotnet-hosting-3.1.0-preview2.19511.6-win.exe
        /// </summary>
        public string LatestLinkShortUrlPrefix { get; }

        public SymbolTargetType SymbolTargetType { get; }

        public TargetFeedConfig(TargetFeedContentType contentType, string targetURL, FeedType type, string token, string latestLinkShortUrlPrefix = null, AssetSelection assetSelection = AssetSelection.All, bool isolated = false, bool @internal = false, bool allowOverwrite = false, SymbolTargetType symbolTargetType = SymbolTargetType.None)
        {
            ContentType = contentType;
            TargetURL = targetURL;
            Type = type;
            Token = token;
            AssetSelection = assetSelection;
            Isolated = isolated;
            Internal = @internal;
            AllowOverwrite = allowOverwrite;
            LatestLinkShortUrlPrefix = latestLinkShortUrlPrefix ?? string.Empty;
            SymbolTargetType = symbolTargetType;
        }

        public override bool Equals(object obj)
        {
            return  
                obj is TargetFeedConfig other &&
                (ContentType == other.ContentType) &&
                TargetURL.Equals(other.TargetURL, StringComparison.OrdinalIgnoreCase) &&
                (Type == other.Type) &&
                Token.Equals(other.Token) &&
                LatestLinkShortUrlPrefix.Equals(other.LatestLinkShortUrlPrefix, StringComparison.OrdinalIgnoreCase) &&
                (AssetSelection == other.AssetSelection) &&
                (Isolated == other.Isolated) &&
                (Internal == other.Internal) &&
                (AllowOverwrite == other.AllowOverwrite) ;
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
        None            = 0,
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

    [Flags]
    public enum SymbolTargetType
    {
        None = 0,
        SymWeb = 1,
        Msdl = 2
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
