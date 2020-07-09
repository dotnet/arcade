// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Build.Tasks.Feed.Model;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public class GeneralTests
    {
        [Fact]
        public void ChannelConfigsHaveAllConfigs()
        {
            foreach (var channelConfig in PublishingConstants.ChannelInfos)
            {
                Assert.True(channelConfig.Id > 0);
                Assert.False(string.IsNullOrEmpty(channelConfig.ShippingFeed));
                Assert.False(string.IsNullOrEmpty(channelConfig.TransportFeed));
                Assert.False(string.IsNullOrEmpty(channelConfig.SymbolsFeed));
                Assert.False(string.IsNullOrEmpty(channelConfig.ChecksumsFeed));
                Assert.False(string.IsNullOrEmpty(channelConfig.InstallersFeed));
            }
        }
    }
}
