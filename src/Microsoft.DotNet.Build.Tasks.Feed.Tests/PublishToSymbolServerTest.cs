using System.Collections.Generic;
using System.IO;
using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
using Microsoft.Arcade.Test.Common;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public class PublishToSymbolServerTest
    {
        private const string MsdlToken = "msdlToken";
        private const string SymWebToken = "SymWebToken";
        private const string TargetUrl = "TargetUrl";
        private const string Msdl = "https://microsoftpublicsymbols.artifacts.visualstudio.com/DefaultCollection";
        private const string SymWeb = "https://microsoft.artifacts.visualstudio.com/DefaultCollection";

        [Theory]
        [InlineData(SymbolTargetType.Msdl, Msdl)]
        [InlineData(SymbolTargetType.SymWeb, SymWeb)]
        public void PublishToSymbolServersTest(SymbolTargetType symbolTargetType , string symbolServer)
        {
            var publishTask = new PublishArtifactsInManifestV3();
            var feedConfigsForSymbols = new HashSet<TargetFeedConfig>();
            feedConfigsForSymbols.Add(new TargetFeedConfig(
                TargetFeedContentType.Symbols,
                "TargetUrl",
                FeedType.AzureStorageFeed,
                MsdlToken,
                string.Empty,
                AssetSelection.All,
                isolated: true,
                @internal: false,
                allowOverwrite: true,
                symbolTargetType));
            Dictionary<string, string> test =
                publishTask.GetTargetSymbolServers(feedConfigsForSymbols, MsdlToken, SymWebToken);
            Assert.True(
                test.ContainsKey(symbolServer));
            Assert.True(test.Count == 1);
        }


        [Fact]
        public void PublishToBothSymbolServerTest()
        {
            var publishTask = new PublishArtifactsInManifestV3();
            var feedConfigsForSymbols = new HashSet<TargetFeedConfig>();
            feedConfigsForSymbols.Add(new TargetFeedConfig(
                TargetFeedContentType.Symbols,
                "testUrl",
                FeedType.AzureStorageFeed,
                SymWebToken,
                string.Empty,
                AssetSelection.All,
                isolated: true,
                @internal: false,
                allowOverwrite: true,
                SymbolTargetType.SymWeb));
            feedConfigsForSymbols.Add(new TargetFeedConfig(
                TargetFeedContentType.Symbols,
                TargetUrl,
                FeedType.AzDoNugetFeed,
                MsdlToken,
                string.Empty,
                AssetSelection.All,
                isolated: true,
                @internal: false,
                allowOverwrite: true,
                SymbolTargetType.Msdl));
            Dictionary<string, string> test =
                publishTask.GetTargetSymbolServers(feedConfigsForSymbols, MsdlToken, SymWebToken);
            Assert.True(
                test.ContainsKey(Msdl));
            Assert.True(test.ContainsKey(SymWeb));
            Assert.True(test.Count == 2);
        }

        [Fact]
        public void TemporarySymbolDirectoryDoesNotExists()
        {
            var buildEngine = new MockBuildEngine();
            var task = new PublishArtifactsInManifestV3()
            {
                BuildEngine = buildEngine,
            };
            var path = TestInputs.GetFullPath("Symbol");
            var publish = task.HandleSymbolPublishingAsync(path, MsdlToken, SymWebToken, "", path, false);
            Assert.True(task.Log.HasLoggedErrors);
        }

        [Fact]
        public void TemporarySymbolsDirectoryTest()
        {
            var publishTask = new PublishArtifactsInManifestV3();
            var path = TestInputs.GetFullPath("Test");
            publishTask.EnsureTemporarySymbolDirectoryExists(path);
            Assert.True(Directory.Exists(path));
            publishTask.DeleteSymbolTemporaryDirectory(path);
            Assert.False(Directory.Exists(path));
        }

        [Fact]
        public void PublishSymbolApiIsCalledTest()
        {
            var publishTask = new PublishArtifactsInManifestV3();
            var path = TestInputs.GetFullPath("Symbols");
            string[] fileEntries = Directory.GetFiles(path);
            var feedConfigsForSymbols = new HashSet<TargetFeedConfig>();
            feedConfigsForSymbols.Add(new TargetFeedConfig(
                TargetFeedContentType.Symbols,
                TargetUrl,
                FeedType.AzureStorageFeed,
                SymWebToken,
                string.Empty,
                AssetSelection.All,
                isolated: true,
                @internal: false,
                allowOverwrite: true,
                SymbolTargetType.SymWeb));
            Dictionary<string, string> test =
                publishTask.GetTargetSymbolServers(feedConfigsForSymbols, MsdlToken, SymWebToken);
            Assert.True(PublishSymbolsHelper.PublishAsync(null,
                path,
                SymWebToken,
                fileEntries,
                fileEntries,
                null,
                365,
                false,
                false,
                null,
                false,
                false,
                false).IsCompleted);
        }

    }
}
