// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public class LatestLinksManagerTests
    {
        [Fact]
        public void FilterAssetsToLink_ExcludesSpecifiedFilenames()
        {
            // Arrange
            var assetsToPublish = new HashSet<string>
            {
                "path/to/file1.zip",
                "path/to/file2.rpm",
                "path/to/file7.rpm",
                "path/to/file3.wixpack.zip",
                "path/to/file4.exe",
                "path/to/file5.symbols.nupkg",
                "path/to/file5.nupkg"
            };
            var filenamesToExclude = new List<string> { "file2.rpm" }.ToImmutableList();

            // Act
            var result = LatestLinksManager.FilterAssetsToLink(assetsToPublish, filenamesToExclude);

            result.Should().BeEquivalentTo(new[] { "path/to/file1.zip", "path/to/file7.rpm", "path/to/file4.exe" });
        }

        [Fact]
        public void FilterAssetsToLink_IncludesCreateLinkPatterns()
        {
            // Arrange
            var assetsToPublish = new HashSet<string>
            {
                "file1.zip",
                "file2.rpm",
                "file3.wixpack",
                "file4.exe"
            };
            var filenamesToExclude = ImmutableList<string>.Empty;

            // Act
            var result = LatestLinksManager.FilterAssetsToLink(assetsToPublish, filenamesToExclude);

            // Assert
            result.Should().BeEquivalentTo(new[] { "file1.zip", "file2.rpm", "file4.exe" });
        }

        [Fact]
        public void FilterAssetsToLink_ExcludesNonMatchingPatterns()
        {
            // Arrange
            var assetsToPublish = new HashSet<string>
            {
                "file1.unknown",
                "file2.unknown"
            };
            var filenamesToExclude = ImmutableList<string>.Empty;

            // Act
            var result = LatestLinksManager.FilterAssetsToLink(assetsToPublish, filenamesToExclude);

            result.Should().BeEmpty();
        }

        [Theory]
        [InlineData("https://dotnetcli.blob.core.windows.net/test", "https://builds.dotnet.microsoft.com/test/")]
        [InlineData("https://dotnetbuilds.blob.core.windows.net/test/", "https://ci.dot.net/test/")]
        public void ComputeLatestLinkBase_WithValidFeedConfig_ReturnsExpectedBaseUrl(string safeTargetUrl, string expectedTargetUrl)
        {
            // Arrange
            var feedConfig = new TargetFeedConfig(
                contentType: TargetFeedContentType.Installer,
                targetURL: safeTargetUrl,
                type: FeedType.AzureStorageContainer,
                token: "dummyToken",
                latestLinkShortUrlPrefixes: ["prefix"]
            );

            // Act
            LatestLinksManager.ComputeLatestLinkBase(feedConfig).Should().Be(expectedTargetUrl);
        }

        [Fact]
        public void ComputeLatestLinkBase_WithTrailingSlash_ReturnsExpectedBaseUrl()
        {
            // Arrange
            var feedConfig = new TargetFeedConfig(
                contentType: TargetFeedContentType.Installer,
                targetURL: "https://dotnetcli.blob.core.windows.net/test/",
                type: FeedType.AzureStorageContainer,
                token: "dummyToken",
                latestLinkShortUrlPrefixes: ["prefix"]
            );

            // Act
            LatestLinksManager.ComputeLatestLinkBase(feedConfig).Should().Be("https://builds.dotnet.microsoft.com/test/");
        }

        [Fact]
        public void ComputeLatestLinkBase_WithUnknownAuthority_ReturnsOriginalBaseUrl()
        {
            // Arrange
            var feedConfig = new TargetFeedConfig(
                contentType: TargetFeedContentType.Installer,
                targetURL: "https://unknown.blob.core.windows.net/test",
                type: FeedType.AzureStorageContainer,
                token: "dummyToken",
                latestLinkShortUrlPrefixes: ["prefix"]
            );

            // Act
            LatestLinksManager.ComputeLatestLinkBase(feedConfig).Should().Be("https://unknown.blob.core.windows.net/test/");
        }
    }
}
