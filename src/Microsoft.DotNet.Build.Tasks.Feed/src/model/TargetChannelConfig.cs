// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;

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

        public TargetChannelConfig(
            int id,
            PublishingInfraVersion publishingInfraVersion,
            string akaMSChannelName,
            string shippingFeed,
            string transportFeed,
            string symbolsFeed,
            string checksumsFeed,
            string installersFeed)
        {
            Id = id;
            PublishingInfraVersion = publishingInfraVersion;
            AkaMSChannelName = akaMSChannelName;
            ShippingFeed = shippingFeed;
            TransportFeed = transportFeed;
            SymbolsFeed = symbolsFeed;
            ChecksumsFeed = checksumsFeed;
            InstallersFeed = installersFeed;
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
                $"\n Checksums-feed: '{ChecksumsFeed}' ";
        }

        public override bool Equals(object other)
        {
            return other is TargetChannelConfig config &&
                   PublishingInfraVersion == config.PublishingInfraVersion &&
                   Id == config.Id &&
                   AkaMSChannelName.Equals(config.AkaMSChannelName, StringComparison.OrdinalIgnoreCase) &&
                   ShippingFeed.Equals(config.ShippingFeed, StringComparison.OrdinalIgnoreCase) &&
                   TransportFeed.Equals(config.TransportFeed, StringComparison.OrdinalIgnoreCase) &&
                   SymbolsFeed.Equals(config.SymbolsFeed, StringComparison.OrdinalIgnoreCase) &&
                   ChecksumsFeed.Equals(config.ChecksumsFeed, StringComparison.OrdinalIgnoreCase) &&
                   InstallersFeed.Equals(config.InstallersFeed, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return (PublishingInfraVersion, 
                Id, 
                AkaMSChannelName, 
                ShippingFeed, 
                TransportFeed, 
                SymbolsFeed, 
                ChecksumsFeed, 
                InstallersFeed).GetHashCode();
        }
    }
}
