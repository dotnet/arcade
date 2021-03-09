// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Feed.Model
{
    /**
     * The goal of this class is to configure a channel that a build can be promoted to,
     * most of the information here is just an extension of the info already present in
     * BAR. However, a big difference in relation to BAR Channel is that this class has
     * the `PublishingInfraVersion` that describes which version of the publishing infra
     * structure the channel configuration is applicable.
     */
    public struct TargetChannelConfig
    {
        /// <summary>
        /// Which version of the publishing infra can use this configuration.
        /// </summary>
        public PublishingInfraVersion PublishingInfraVersion { get; }

        /// <summary>
        /// BAR Channel ID
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// The name that should be used for creating Aka.ms links for this channel.
        /// </summary>
        public string AkaMSChannelName { get; }

        /// <summary>
        /// The URL (including the index.json suffix) of the *shipping* feed to be used for this channel.
        /// </summary>
        public string ShippingFeed { get; }

        /// <summary>
        /// The URL (including the index.json suffix) of the *transport* feed to be used for this channel.
        /// </summary>
        public string TransportFeed { get; }

        /// <summary>
        /// The URL (including the index.json suffix) of the *symbols* feed to be used for this channel.
        /// </summary>
        public string SymbolsFeed { get; }

        /// <summary>
        /// The URL (including the index.json suffix) where *checksums* should be published to.
        /// </summary>
        public string ChecksumsFeed { get; }

        /// <summary>
        /// The URL (including the index.json suffix) where *installers* should be published to.
        /// </summary>
        public string InstallersFeed { get; }

        /// <summary>
        /// Should publish to Msdl
        /// </summary>
        public SymbolTargetType SymbolTargetType { get; }

        public bool IsInternal { get; }

        public List<string> FilenamesToExclude { get; }

        public bool Flatten { get; }

        public TargetChannelConfig(
            int id,
            bool isInternal,
            PublishingInfraVersion publishingInfraVersion,
            string akaMSChannelName,
            string shippingFeed,
            string transportFeed,
            string symbolsFeed,
            string checksumsFeed,
            string installersFeed,
            SymbolTargetType symbolTargetType,
            List<string> filenamesToExclude = null,
            bool flatten = true)
        {
            Id = id;
            IsInternal = isInternal;
            PublishingInfraVersion = publishingInfraVersion;
            AkaMSChannelName = akaMSChannelName;
            ShippingFeed = shippingFeed;
            TransportFeed = transportFeed;
            SymbolsFeed = symbolsFeed;
            ChecksumsFeed = checksumsFeed;
            InstallersFeed = installersFeed;
            SymbolTargetType = symbolTargetType;
            FilenamesToExclude = filenamesToExclude ?? new List<string>();
            Flatten = flatten;
        }

        public override string ToString()
        {
            return
                $"\n Channel ID: '{Id}' " +
                $"\n Infra-version: '{PublishingInfraVersion}' " +
                $"\n AkaMSChannelName: '{AkaMSChannelName}' " +
                $"\n Shipping-feed: '{ShippingFeed}' " +
                $"\n Transport-feed: '{TransportFeed}' " +
                $"\n Symbols-feed: '{SymbolsFeed}' " +
                $"\n Installers-feed: '{InstallersFeed}' " +
                $"\n Checksums-feed: '{ChecksumsFeed}' " +
                $"\n SymbolTargetType: '{SymbolTargetType}' " +
                $"\n IsInternal: '{IsInternal}'" +
                $"\n FilenamesToExclude: \n\t{string.Join("\n\t", FilenamesToExclude)}" +
                $"\n Flatten: '{Flatten}'";
        }

        public override bool Equals(object other)
        {
            if (other is TargetChannelConfig config &&
                PublishingInfraVersion == config.PublishingInfraVersion &&
                Id == config.Id &&
                String.Equals(AkaMSChannelName, config.AkaMSChannelName, StringComparison.OrdinalIgnoreCase) &&
                String.Equals(ShippingFeed, config.ShippingFeed, StringComparison.OrdinalIgnoreCase) &&
                String.Equals(TransportFeed, config.TransportFeed, StringComparison.OrdinalIgnoreCase) &&
                String.Equals(SymbolsFeed, config.SymbolsFeed, StringComparison.OrdinalIgnoreCase) &&
                String.Equals(ChecksumsFeed, config.ChecksumsFeed, StringComparison.OrdinalIgnoreCase) &&
                String.Equals(InstallersFeed, config.InstallersFeed, StringComparison.OrdinalIgnoreCase) &&
                IsInternal == config.IsInternal &&
                Flatten == config.Flatten)
            {
                if (FilenamesToExclude is null)
                    return config.FilenamesToExclude is null;
                
                if (config.FilenamesToExclude is null)
                    return false;
                
                return FilenamesToExclude.SequenceEqual(config.FilenamesToExclude);
            }
            
            return false;
        }

        public override int GetHashCode()
        {
            return (PublishingInfraVersion, 
                Id, 
                IsInternal,
                AkaMSChannelName, 
                ShippingFeed, 
                TransportFeed, 
                SymbolsFeed, 
                ChecksumsFeed, 
                InstallersFeed,
                SymbolTargetType,
                Flatten,
                string.Join(" ", FilenamesToExclude)).GetHashCode();
        }
    }
}
