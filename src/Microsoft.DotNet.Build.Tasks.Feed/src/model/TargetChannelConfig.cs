// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        public TargetChannelConfig(
            int id,
            PublishingInfraVersion publishingInfraVersion,
            string name,
            string akaMSChannelName,
            string shippingFeed,
            string transportFeed,
            string symbolsFeed)
        {
            Id = id;
            PublishingInfraVersion = publishingInfraVersion;
            Name = name;
            AkaMSChannelName = akaMSChannelName;
            ShippingFeed = shippingFeed;
            TransportFeed = transportFeed;
            SymbolsFeed = symbolsFeed;
        }
    }
}
