// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.BuildManifest.Model;

namespace Microsoft.DotNet.Build.Tasks.Feed.Model
{
    public struct TargetChannelConfig
    {
        public int Id { get; }
        public PublishingInfraVersion PublishingInfraVersion { get; }
        public string Name { get; }
        public string AkaMSChannelName { get; }
        public string ShippingFeed { get; }
        public string TransportFeed { get; }
        public string SymbolsFeed { get; }
        public string ChecksumsFeed { get; }
        public string InstallersFeed { get; }

        public TargetChannelConfig(
            int id,
            PublishingInfraVersion publishingInfraVersion,
            string name,
            string akaMSChannelName,
            string shippingFeed,
            string transportFeed,
            string symbolsFeed,
            string checksumsFeed,
            string installersFeed)
        {
            Id = id;
            PublishingInfraVersion = publishingInfraVersion;
            Name = name;
            AkaMSChannelName = akaMSChannelName;
            ShippingFeed = shippingFeed;
            TransportFeed = transportFeed;
            SymbolsFeed = symbolsFeed;
            ChecksumsFeed = checksumsFeed;
            InstallersFeed = installersFeed;
        }
    }
}
