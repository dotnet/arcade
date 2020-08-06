// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Build.Tasks.Feed.Model;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public class SetupTargetFeedConfigV3Tests
    {
        private const string AzureStorageTargetFeedPAT = "AzureStorageTargetFeedPAT";
        private const string LatestLinkShortUrlPrefix = "LatestLinkShortUrlPrefix";
        private const string AzureDevOpsFeedsKey = "AzureDevOpsFeedsKey";

        private const string StablePackageFeed = "StablePackageFeed";
        private const string StableSymbolsFeed = "StableSymbolsFeed";

        private const string ChecksumsTargetStaticFeed = "ChecksumsTargetStaticFeed";
        private const string ChecksumsTargetStaticFeedKey = "ChecksumsTargetStaticFeedKey";

        private const string InstallersTargetStaticFeed = "InstallersTargetStaticFeed";
        private const string InstallersTargetStaticFeedKey = "InstallersTargetStaticFeedKey";

        private const string AzureDevOpsStaticShippingFeed = "AzureDevOpsStaticShippingFeed";
        private const string AzureDevOpsStaticTransportFeed = "AzureDevOpsStaticTransportFeed";
        private const string AzureDevOpsStaticSymbolsFeed = "AzureDevOpsStaticSymbolsFeed";

        private readonly List<TargetFeedContentType> Installers = new List<TargetFeedContentType>() { 
            TargetFeedContentType.OSX,
            TargetFeedContentType.Deb,
            TargetFeedContentType.Rpm,
            TargetFeedContentType.Node,
            TargetFeedContentType.BinaryLayout,
            TargetFeedContentType.Installer,
            TargetFeedContentType.Maven,
            TargetFeedContentType.VSIX,
            TargetFeedContentType.Badge,
            TargetFeedContentType.Other
        };

        private readonly ITestOutputHelper Output;

        public SetupTargetFeedConfigV3Tests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void StableFeeds(bool publishInstallersAndChecksums, bool isInternalBuild)
        {
            var expectedFeeds = new List<TargetFeedConfig>();

            if (publishInstallersAndChecksums)
            {
                expectedFeeds.Add(
                    new TargetFeedConfig(
                        TargetFeedContentType.Checksum,
                        ChecksumsTargetStaticFeed,
                        FeedType.AzureStorageFeed,
                        ChecksumsTargetStaticFeedKey,
                        string.Empty,
                        AssetSelection.All,
                        isolated: true,
                        @internal: false,
                        allowOverwrite: true));

                foreach (var contentType in Installers)
                {
                    expectedFeeds.Add(
                        new TargetFeedConfig(
                            contentType,
                            InstallersTargetStaticFeed,
                            FeedType.AzureStorageFeed,
                            InstallersTargetStaticFeedKey,
                            string.Empty,
                            AssetSelection.All,
                            isolated: true,
                            @internal: false,
                            allowOverwrite: true));
                }
            }

            expectedFeeds.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    StablePackageFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    string.Empty,
                    AssetSelection.ShippingOnly,
                    isolated: true,
                    @internal: false,
                    allowOverwrite: false));

            expectedFeeds.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Symbols,
                    StableSymbolsFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    string.Empty,
                    AssetSelection.All,
                    isolated: true,
                    @internal: false,
                    allowOverwrite: false));

            expectedFeeds.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    AzureDevOpsStaticTransportFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    string.Empty,
                    AssetSelection.NonShippingOnly,
                    isolated: false,
                    @internal: false,
                    allowOverwrite: false));

            var buildEngine = new MockBuildEngine();
            var config = new SetupTargetFeedConfigV3(
                    isInternalBuild,
                    isStableBuild: true,
                    repositoryName: "test-repo",
                    commitSha: "c0c0c0c0",
                    AzureStorageTargetFeedPAT,
                    publishInstallersAndChecksums,
                    InstallersTargetStaticFeed,
                    InstallersTargetStaticFeedKey,
                    ChecksumsTargetStaticFeed,
                    ChecksumsTargetStaticFeedKey,
                    AzureDevOpsStaticShippingFeed,
                    AzureDevOpsStaticTransportFeed,
                    AzureDevOpsStaticSymbolsFeed,
                    LatestLinkShortUrlPrefix,
                    AzureDevOpsFeedsKey,
                    buildEngine,
                    StablePackageFeed,
                    StableSymbolsFeed
                );

            var actualFeeds = config.Setup();

            Assert.True(AreEquivalent(expectedFeeds, actualFeeds));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NonStableAndInternal(bool publishInstallersAndChecksums)
        {
            var expectedFeeds = new List<TargetFeedConfig>();

            if (publishInstallersAndChecksums)
            {
                foreach (var contentType in Installers)
                {
                    expectedFeeds.Add(
                        new TargetFeedConfig(
                            contentType,
                            InstallersTargetStaticFeed,
                            FeedType.AzureStorageFeed,
                            InstallersTargetStaticFeedKey,
                            string.Empty,
                            AssetSelection.All,
                            isolated: false,
                            @internal: true,
                            allowOverwrite: false));
                }

                expectedFeeds.Add(
                    new TargetFeedConfig(
                        TargetFeedContentType.Checksum,
                        ChecksumsTargetStaticFeed,
                        FeedType.AzureStorageFeed,
                        ChecksumsTargetStaticFeedKey,
                        string.Empty,
                        AssetSelection.All,
                        isolated: false,
                        @internal: true,
                        allowOverwrite: false));

            }

            expectedFeeds.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    AzureDevOpsStaticShippingFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    string.Empty,
                    AssetSelection.ShippingOnly,
                    isolated: false,
                    @internal: true,
                    allowOverwrite: false));

            expectedFeeds.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    AzureDevOpsStaticTransportFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    string.Empty,
                    AssetSelection.NonShippingOnly,
                    isolated: false,
                    @internal: true,
                    allowOverwrite: false));

            expectedFeeds.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Symbols,
                    AzureDevOpsStaticSymbolsFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    string.Empty,
                    AssetSelection.All,
                    isolated: false,
                    @internal: true,
                    allowOverwrite: false));

            var buildEngine = new MockBuildEngine();
            var config = new SetupTargetFeedConfigV3(
                    isInternalBuild: true,
                    isStableBuild: false,
                    repositoryName: "test-repo",
                    commitSha: "c0c0c0c0",
                    AzureStorageTargetFeedPAT,
                    publishInstallersAndChecksums,
                    InstallersTargetStaticFeed,
                    InstallersTargetStaticFeedKey,
                    ChecksumsTargetStaticFeed,
                    ChecksumsTargetStaticFeedKey,
                    AzureDevOpsStaticShippingFeed,
                    AzureDevOpsStaticTransportFeed,
                    AzureDevOpsStaticSymbolsFeed,
                    LatestLinkShortUrlPrefix,
                    AzureDevOpsFeedsKey,
                    buildEngine: buildEngine
                );

            var actualFeeds = config.Setup();

            Assert.True(AreEquivalent(expectedFeeds, actualFeeds));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void NonStableAndPublic(bool publishInstallersAndChecksums)
        {
            var expectedFeeds = new List<TargetFeedConfig>();

            if (publishInstallersAndChecksums)
            {
                expectedFeeds.Add(
                    new TargetFeedConfig(
                        TargetFeedContentType.Checksum,
                        ChecksumsTargetStaticFeed,
                        FeedType.AzureStorageFeed,
                        ChecksumsTargetStaticFeedKey,
                        LatestLinkShortUrlPrefix,
                        AssetSelection.All,
                        isolated: false,
                        @internal: false,
                        allowOverwrite: false));

                foreach (var contentType in Installers)
                {
                    expectedFeeds.Add(
                        new TargetFeedConfig(
                            contentType,
                            InstallersTargetStaticFeed,
                            FeedType.AzureStorageFeed,
                            InstallersTargetStaticFeedKey,
                            LatestLinkShortUrlPrefix,
                            AssetSelection.All,
                            isolated: false,
                            @internal: false,
                            allowOverwrite: false));
                }
            }

            expectedFeeds.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Symbols,
                    PublishingConstants.LegacyDotNetBlobFeedURL,
                    FeedType.AzureStorageFeed,
                    AzureStorageTargetFeedPAT,
                    string.Empty,
                    AssetSelection.All,
                    isolated: false,
                    @internal: false,
                    allowOverwrite: false));

            expectedFeeds.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    AzureDevOpsStaticShippingFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    string.Empty,
                    AssetSelection.ShippingOnly,
                    isolated: false,
                    @internal: false,
                    allowOverwrite: false));

            expectedFeeds.Add(
                new TargetFeedConfig(
                    TargetFeedContentType.Package,
                    AzureDevOpsStaticTransportFeed,
                    FeedType.AzDoNugetFeed,
                    AzureDevOpsFeedsKey,
                    string.Empty,
                    AssetSelection.NonShippingOnly,
                    isolated: false,
                    @internal: false,
                    allowOverwrite: false));

            var buildEngine = new MockBuildEngine();
            var config = new SetupTargetFeedConfigV3(
                    isInternalBuild: false,
                    isStableBuild: false,
                    repositoryName: "test-repo",
                    commitSha: "c0c0c0c0",
                    AzureStorageTargetFeedPAT,
                    publishInstallersAndChecksums,
                    InstallersTargetStaticFeed,
                    InstallersTargetStaticFeedKey,
                    ChecksumsTargetStaticFeed,
                    ChecksumsTargetStaticFeedKey,
                    AzureDevOpsStaticShippingFeed,
                    AzureDevOpsStaticTransportFeed,
                    AzureDevOpsStaticSymbolsFeed,
                    LatestLinkShortUrlPrefix,
                    AzureDevOpsFeedsKey,
                    buildEngine: buildEngine
                );

            var actualFeeds = config.Setup();

            Assert.True(AreEquivalent(expectedFeeds, actualFeeds));
        }
    
        private bool AreEquivalent(List<TargetFeedConfig> expectedItems, List<TargetFeedConfig> actualItems) 
        {
            if (expectedItems.Count() != actualItems.Count())
            {
                Output.WriteLine($"The expected items list has {expectedItems.Count()} item(s) but the list of actual items has {actualItems.Count()}.");

                return false;
            }

            foreach (var expected in expectedItems)
            {
                if (actualItems.Contains(expected) == false)
                {
                    Output.WriteLine($"Expected item was not found in the actual collection of items: {expected}");

                    Output.WriteLine("Actual items are: ");
                    foreach (var actual in actualItems)
                    {
                        Output.WriteLine($"\t {actual}");
                    }

                    return false;
                }
            }

            return true;
        }
    }
}
