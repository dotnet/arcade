// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Arcade.Common;
using Microsoft.Arcade.Test.Common;
using Microsoft.DotNet.Arcade.Test.Common;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
using Microsoft.DotNet.Maestro.Client.Models;
using Xunit;
using static Microsoft.DotNet.Internal.SymbolHelper.SymbolPromotionHelper;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public class PublishToSymbolServerTest
    {
        private const string TargetUrl = "TargetUrl";

        [Fact]
        public void TemporarySymbolsDirectoryTest()
        {
            var buildEngine = new MockBuildEngine();
            var publishTask = new PublishArtifactsInManifestV3()
            {
                BuildEngine = buildEngine,
            };
            var path = TestInputs.GetFullPath("Test");
            publishTask.EnsureTemporaryDirectoryExists(path);
            Assert.True(Directory.Exists(path));
            publishTask.DeleteTemporaryDirectory(path);
            Assert.False(Directory.Exists(path));
        }

        [Theory]
        [InlineData(SymbolPublishVisibility.Public)]
        [InlineData(SymbolPublishVisibility.Internal)]
        public void PublishToSymbolServersTest(SymbolPublishVisibility symbolTargetVisibility)
        {
            var publishTask = new PublishArtifactsInManifestV3();
            var feedConfigsForSymbols = new HashSet<TargetFeedConfig>
            {
                new TargetFeedConfig(
                TargetFeedContentType.Symbols,
                "TargetUrl",
                FeedType.AzDoNugetFeed,
                default,
                new List<string>(),
                AssetSelection.All,
                isolated: true,
                @internal: false,
                allowOverwrite: true,
                symbolTargetVisibility)
            };
            SymbolPublishVisibility visibility = publishTask.GetSymbolPublishingVisibility(feedConfigsForSymbols);
            Assert.Equal(symbolTargetVisibility, visibility);
        }

        [Fact]
        public void EnsureOrderingOfVisibility()
        {
            Assert.True(SymbolPublishVisibility.Public > SymbolPublishVisibility.Internal);
            Assert.True(SymbolPublishVisibility.Internal > SymbolPublishVisibility.None);

            Assert.True(Visibility.Public > Visibility.Internal);
        }

        [Theory]
        [InlineData(SymbolPublishVisibility.None)]
        [InlineData(SymbolPublishVisibility.Internal)]
        [InlineData(SymbolPublishVisibility.Public)]
        public void PublishToMultipleServersVisibilityTest(SymbolPublishVisibility maxVisibility)
        {
            PublishArtifactsInManifestV3 publishTask = new();
            HashSet<TargetFeedConfig> feedConfigsForSymbols = [];
            IEnumerable<SymbolPublishVisibility> visibilities = Enum.GetValues<SymbolPublishVisibility>().Where(x => x <= maxVisibility);
            foreach (SymbolPublishVisibility v in visibilities)
            {
                feedConfigsForSymbols.Add(
                    new TargetFeedConfig(
                    TargetFeedContentType.Symbols,
                    "testUrl" + v,
                    FeedType.AzDoNugetFeed,
                    default,
                    [],
                    AssetSelection.All,
                    isolated: true,
                    @internal: false,
                    allowOverwrite: true,
                    v));
            }
            SymbolPublishVisibility visibility = publishTask.GetSymbolPublishingVisibility(feedConfigsForSymbols);
            Assert.Equal(maxVisibility, visibility);
        }

        [Fact]
        public async Task NoAssetsToPublishFound()
        {
            (var buildEngine, var task, _, _, _, var buildInfo) = GetCanonicalSymbolTestAssets();

            var path = TestInputs.GetFullPath("Symbol");
            var buildAsset = ReadOnlyDictionary<string, Asset>.Empty;

            await task.HandleSymbolPublishingAsync(
                buildInfo: buildInfo,
                buildAssets: buildAsset,
                pdbArtifactsBasePath: path,
                symbolPublishingExclusionsFile: "",
                publishSpecialClrFiles: false,
                clientThrottle: null);

            // TODO: Should this be a warning at least? it used to be an error but it doesn't make as much sense.
            // This isn't the best type of test as it tests specifics and not behavior, but the task doesn't
            // have interesting observable state.
            Assert.Contains(buildEngine.BuildMessageEvents, x => x.Message.StartsWith("No assets to publish"));
        }

        [Fact]
        public async Task NoServersToPublishFound()
        {
            (var buildEngine, var task, var symbolPackages, _, _, var buildInfo) = GetCanonicalSymbolTestAssets(SymbolPublishVisibility.None);

            await task.HandleSymbolPublishingAsync(
                buildInfo: buildInfo,
                symbolPackages,
                pdbArtifactsBasePath: null,
                symbolPublishingExclusionsFile: "",
                publishSpecialClrFiles: false,
                clientThrottle: null);

            // This isn't the best type of test as it tests implementation specifics and not behavior, but the task doesn't
            // have interesting observable state. It's also a big perf hit to try to not go down this path. As a design decision,
            // if symbl type is none we don't even publish to the home/temp tenant.
            Assert.Contains(buildEngine.BuildMessageEvents, x => x.Message.StartsWith("No target symbol servers"));
        }

        [WindowsOnlyFact]
        public async Task PublishSymbolsBasicScenarioTest()
        {
            (var buildEngine, var task, var symbolPackages, var symbolFilesDir, var exclusionFile, var buildInfo) = GetCanonicalSymbolTestAssets(SymbolPublishVisibility.Public);

            await task.HandleSymbolPublishingAsync(
                buildInfo: buildInfo,
                symbolPackages,
                pdbArtifactsBasePath: symbolFilesDir,
                symbolPublishingExclusionsFile: exclusionFile,
                publishSpecialClrFiles: false,
                clientThrottle: null,
                dryRun: true,
                Internal.SymbolHelper.SymbolPromotionHelper.Environment.PPE);

            Assert.Empty(buildEngine.BuildErrorEvents);
            Assert.Empty(buildEngine.BuildWarningEvents);

            // This isn't the best type of test as it tests implementation specifics and not behavior, but the task doesn't
            // have interesting observable state.
            Assert.Contains(buildEngine.BuildMessageEvents, x => x.Message.StartsWith("Publishing Symbols to Symbol server"));
            var message = buildEngine.BuildMessageEvents.Single(x => x.Message.StartsWith("Publishing Symbols to Symbol server"));
            Assert.Contains("Symbol package count: 1", message.Message);
            Assert.Contains("Loose symbol file count: 1", message.Message);

            Assert.Contains(buildEngine.BuildMessageEvents, x => x.Message.Contains("Creating symbol request"));
            Assert.Equal(2, buildEngine.BuildMessageEvents.Where(x => x.Message.Contains("Adding directory")).Count());

            // Message per package per server
            Assert.Equal(symbolPackages.Keys.Count, buildEngine.BuildMessageEvents.Where(x => x.Message.Contains($"Extracting symbol package")).Count());
            Assert.Equal(symbolPackages.Keys.Count, buildEngine.BuildMessageEvents.Where(x => x.Message.Contains($"Adding package")).Count());

            // Make sure exclusions are tracked this should change in conjunction with the exclusion file in the symbols test directory.
            Assert.Contains(buildEngine.BuildMessageEvents , x => x.Message.Contains("Skipping lib/net8.0/aztoken.dll"));
            Assert.Contains(buildEngine.BuildMessageEvents, x => x.Message.StartsWith("Finished publishing symbols to temporary azdo org"));
            Assert.Single(buildEngine.BuildMessageEvents, x => x.Message.StartsWith("Would register request"));
            Microsoft.Build.Framework.BuildMessageEventArgs registerLog = buildEngine.BuildMessageEvents.Where(x => x.Message.StartsWith("Would register request")).Single();
            Assert.Contains("project dotnettest", registerLog.Message);
            Assert.Contains("environment PPE", registerLog.Message);
            Assert.Contains("visibility Public", registerLog.Message);
            Assert.Contains("to last 3650 days", registerLog.Message);
        }

        private static (MockBuildEngine, PublishArtifactsInManifestV3, ReadOnlyDictionary<string, Asset>, string, string, Maestro.Client.Models.Build) GetCanonicalSymbolTestAssets(SymbolPublishVisibility targetServer = SymbolPublishVisibility.Public)
        {
            var symbolPackages = new Dictionary<string, Asset>()
            {
                { "test-package-a.1.0.0.symbols.nupkg",  new(id: 0, buildId: 0, nonShipping: true, name: "test-package-a.1.0.0.symbols.nupkg", version: "1.0.0", locations: [])}
            }.AsReadOnly();
            var exclusionFile = TestInputs.GetFullPath("Symbols/SymbolPublishingExclusionsFile.txt");
            var symbolFilesDir = TestInputs.GetFullPath("Symbols");

            var buildEngine = new MockBuildEngine();

            HashSet<TargetFeedConfig> feedConfigsForSymbols = [
                new TargetFeedConfig(
                TargetFeedContentType.Symbols,
                TargetUrl,
                FeedType.AzDoNugetFeed,
                default,
                latestLinkShortUrlPrefixes: [],
                AssetSelection.All,
                isolated: true,
                @internal: false,
                allowOverwrite: true,
                targetServer)
            ];

            var task = new PublishArtifactsInManifestV3()
            {
                BuildEngine = buildEngine,
                ArtifactsBasePath = "testPath",
                BlobAssetsBasePath = symbolFilesDir,
                TempSymbolsAzureDevOpsOrg = "dncengtest",
                TempSymbolsAzureDevOpsOrgToken = "token",
                SymbolRequestProject = "dotnettest"
            };
            task.FeedConfigs.Add(TargetFeedContentType.Symbols, feedConfigsForSymbols);

            Maestro.Client.Models.Build buildInfo = new(id: 4242, DateTimeOffset.Now, staleness: 0, released: false, stable: true, commit: "abcd", [], [], [], []);

            return (buildEngine, task, symbolPackages, symbolFilesDir, exclusionFile, buildInfo);
        }

        [Fact]
        public void DownloadFileAsyncSucceedsForValidUrl()
        {
            var buildEngine = new MockBuildEngine();
            var publishTask = new PublishArtifactsInManifestV3
            {
                BuildEngine = buildEngine,
            };

            var testFile = Path.Combine("Symbols", "test.txt");
            var responseContent = TestInputs.ReadAllBytes(testFile);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(responseContent)
            };

            using HttpClient client = FakeHttpClient.WithResponses(response);
            var path = TestInputs.GetFullPath(Guid.NewGuid().ToString());

            var test = publishTask.DownloadFileAsync(
                client,
                PublishArtifactsInManifestBase.ArtifactName.BlobArtifacts,
                "1234",
                "test.txt",
                path);

            Assert.True(File.Exists(path));
            publishTask.DeleteTemporaryFiles(path);
            publishTask.DeleteTemporaryDirectory(path);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.GatewayTimeout)]
        public async Task DownloadFileAsyncFailsForInValidUrlTest(HttpStatusCode httpStatus)
        {
            var buildEngine = new MockBuildEngine();
            var publishTask = new PublishArtifactsInManifestV3
            {
                BuildEngine = buildEngine,
            };
            var testFile = Path.Combine("Symbols", "test.txt");
            var responseContent = TestInputs.ReadAllBytes(testFile);
            publishTask.RetryHandler = new ExponentialRetry() { MaxAttempts = 3, DelayBase = 1 };

            var responses = new[]
            {
                new HttpResponseMessage(httpStatus)
                {
                    Content = new ByteArrayContent(responseContent)
                },
                new HttpResponseMessage(httpStatus)
                {
                    Content = new ByteArrayContent(responseContent)
                },
                new HttpResponseMessage(httpStatus)
                {
                    Content = new ByteArrayContent(responseContent)
                }
            };
            using HttpClient client = FakeHttpClient.WithResponses(responses);
            var path = TestInputs.GetFullPath(Guid.NewGuid().ToString());

            var actualError = await Assert.ThrowsAsync<Exception>(() =>
                publishTask.DownloadFileAsync(
                    client,
                    PublishArtifactsInManifestBase.ArtifactName.BlobArtifacts,
                    "1234",
                    "test.txt",
                    path));
            Assert.Contains($"Failed to download local file '{path}' after {publishTask.RetryHandler.MaxAttempts} attempts.  See inner exception for details.", actualError.Message);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.GatewayTimeout)]
        public async Task DownloadFailureWhenStatusCodeIsInvalid(HttpStatusCode httpStatus)
        {
            var buildEngine = new MockBuildEngine();
            var publishTask = new PublishArtifactsInManifestV3
            {
                BuildEngine = buildEngine,
            };
            var testFile = Path.Combine("Symbols", "test.txt");
            var responseContent = TestInputs.ReadAllBytes(testFile);
            publishTask.RetryHandler = new ExponentialRetry() { MaxAttempts = 3, DelayBase = 1 };

            var responses = new[]
            {
                new HttpResponseMessage(httpStatus)
                {
                    Content = new ByteArrayContent(responseContent)
                },
                new HttpResponseMessage(httpStatus)
                {
                    Content = new ByteArrayContent(responseContent)
                },
                new HttpResponseMessage(httpStatus)
                {
                    Content = new ByteArrayContent(responseContent)
                }
            };
            using HttpClient client = FakeHttpClient.WithResponses(responses);
            var path = TestInputs.GetFullPath(Guid.NewGuid().ToString());

            var actualError = await Assert.ThrowsAsync<Exception>(() =>
                publishTask.DownloadFileAsync(
                    client,
                    PublishArtifactsInManifestBase.ArtifactName.BlobArtifacts,
                    "1234",
                    "test.txt",
                    path));
            Assert.Contains($"Failed to download local file '{path}' after {publishTask.RetryHandler.MaxAttempts} attempts.  See inner exception for details.", actualError.Message);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.GatewayTimeout)]
        public async Task DownloadFileSuccessfulAfterRetryTest(HttpStatusCode httpStatus)
        {
            var buildEngine = new MockBuildEngine();
            var publishTask = new PublishArtifactsInManifestV3
            {
                BuildEngine = buildEngine,
            };
            var testFile = Path.Combine("Symbols", "test.txt");
            var responseContent = TestInputs.ReadAllBytes(testFile);
            publishTask.RetryHandler = new ExponentialRetry() { MaxAttempts = 2, DelayBase = 1 };

            var responses = new[]
            {
                new HttpResponseMessage(httpStatus)
                {
                    Content = new ByteArrayContent(responseContent)
                },
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(responseContent)
                }
            };
            using HttpClient client = FakeHttpClient.WithResponses(responses);
            var path = TestInputs.GetFullPath(Guid.NewGuid().ToString());

            await publishTask.DownloadFileAsync(
                client,
                PublishArtifactsInManifestBase.ArtifactName.BlobArtifacts,
                "1234",
                "test.txt",
                path);
            Assert.True(File.Exists(path));
            publishTask.DeleteTemporaryFiles(path);
            publishTask.DeleteTemporaryDirectory(path);
        }

        [Theory]
        [InlineData(PublishArtifactsInManifestBase.ArtifactName.BlobArtifacts, "1")]
        [InlineData(PublishArtifactsInManifestBase.ArtifactName.PackageArtifacts, "1234")]
        public async Task GetContainerIdToDownloadArtifactAsync(PublishArtifactsInManifestBase.ArtifactName artifactName, string containerId)
        {
            var buildEngine = new MockBuildEngine();
            var publishTask = new PublishArtifactsInManifestV3
            {
                BuildEngine = buildEngine,
            };
            publishTask.BuildId = "1243456";
            var testPackageName = Path.Combine("Symbols", "test.txt");
            var responseContent = TestInputs.ReadAllBytes(testPackageName);
            var responses = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(responseContent)
            };

            using HttpClient client = FakeHttpClient.WithResponses(responses);
            var test = await publishTask.GetContainerIdAsync(
                client,
                artifactName);
            Assert.Equal(containerId, test);
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task ErrorAfterMaxRetriesToGetContainerId(HttpStatusCode httpStatus)
        {
            var buildEngine = new MockBuildEngine();
            var publishTask = new PublishArtifactsInManifestV3
            {
                BuildEngine = buildEngine,
            };
            publishTask.BuildId = "1243456";
            publishTask.RetryHandler = new ExponentialRetry() {MaxAttempts = 3, DelayBase = 1};

            var testPackageName = Path.Combine("Symbols", "test.txt");
            var responseContent = TestInputs.ReadAllBytes(testPackageName);
            var responses = new[]
            {
                new HttpResponseMessage(httpStatus)
                {
                    Content = new ByteArrayContent(responseContent)
                },
                new HttpResponseMessage(httpStatus)
                {
                    Content = new ByteArrayContent(responseContent)
                },
                new HttpResponseMessage(httpStatus)
                {
                    Content = new ByteArrayContent(responseContent)
                }
            };

            using HttpClient client = FakeHttpClient.WithResponses(responses);

            var actualError = await Assert.ThrowsAsync<Exception>(() =>
                publishTask.GetContainerIdAsync(
                    client,
                    PublishArtifactsInManifestBase.ArtifactName.BlobArtifacts));
            Assert.Contains($"Failed to get container id after {publishTask.RetryHandler.MaxAttempts} attempts.  See inner exception for details,", actualError.Message);
        }
    }
}
