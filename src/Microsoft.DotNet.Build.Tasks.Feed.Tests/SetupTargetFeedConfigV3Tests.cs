// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Arcade.Test.Common;
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
        private const string BuildQuality = "quality";
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

        private static List<string> FilesToExclude = new List<string>() { 
            "filename",
            "secondfilename"
        };

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

        private const SymbolTargetType symbolTargetType = SymbolTargetType.None;

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
                        allowOverwrite: true,
                        symbolTargetType,
                        filenamesToExclude: FilesToExclude));

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
                            allowOverwrite: true,
                            symbolTargetType,
                            filenamesToExclude: FilesToExclude));
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
                    allowOverwrite: false,
                    symbolTargetType,
                    filenamesToExclude: FilesToExclude));

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
                    allowOverwrite: false,
                    symbolTargetType,
                    filenamesToExclude: FilesToExclude));

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
                    allowOverwrite: false,
                    symbolTargetType,
                    filenamesToExclude: FilesToExclude));

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
                    $"{LatestLinkShortUrlPrefix}/{BuildQuality}",
                    AzureDevOpsFeedsKey,
                    buildEngine,
                    symbolTargetType,
                    StablePackageFeed,
                    StableSymbolsFeed,
                    filesToExclude: FilesToExclude
                );

            var actualFeeds = config.Setup();

            actualFeeds.Should().BeEquivalentTo(expectedFeeds);
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
                            allowOverwrite: false,
                            symbolTargetType,
                            filenamesToExclude: FilesToExclude));
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
                        allowOverwrite: false,
                        symbolTargetType,
                        filenamesToExclude: FilesToExclude));

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
                    allowOverwrite: false,
                    symbolTargetType,
                    filenamesToExclude: FilesToExclude));

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
                    allowOverwrite: false,
                    symbolTargetType,
                    filenamesToExclude: FilesToExclude));

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
                    allowOverwrite: false,
                    symbolTargetType,
                    filenamesToExclude: FilesToExclude));

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
                    $"{LatestLinkShortUrlPrefix}/{BuildQuality}",
                    AzureDevOpsFeedsKey,
                    buildEngine: buildEngine,
                    symbolTargetType,
                    filesToExclude: FilesToExclude
                );

            var actualFeeds = config.Setup();

            actualFeeds.Should().BeEquivalentTo(expectedFeeds);
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
                        $"{LatestLinkShortUrlPrefix}/{BuildQuality}",
                        AssetSelection.All,
                        isolated: false,
                        @internal: false,
                        allowOverwrite: false,
                        symbolTargetType,
                        filenamesToExclude: FilesToExclude));

                foreach (var contentType in Installers)
                {
                    expectedFeeds.Add(
                        new TargetFeedConfig(
                            contentType,
                            InstallersTargetStaticFeed,
                            FeedType.AzureStorageFeed,
                            InstallersTargetStaticFeedKey,
                            $"{LatestLinkShortUrlPrefix}/{BuildQuality}",
                            AssetSelection.All,
                            isolated: false,
                            @internal: false,
                            allowOverwrite: false,
                            symbolTargetType,
                            filenamesToExclude: FilesToExclude));
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
                    allowOverwrite: false,
                    symbolTargetType,
                    filenamesToExclude: FilesToExclude));

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
                    allowOverwrite: false,
                    symbolTargetType,
                    filenamesToExclude: FilesToExclude));

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
                    allowOverwrite: false,
                    symbolTargetType,
                    filenamesToExclude: FilesToExclude));

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
                    $"{LatestLinkShortUrlPrefix}/{BuildQuality}",
                    AzureDevOpsFeedsKey,
                    buildEngine: buildEngine,
                    symbolTargetType,
                    filesToExclude: FilesToExclude
                );

            var actualFeeds = config.Setup();

            actualFeeds.Should().BeEquivalentTo(expectedFeeds);
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
