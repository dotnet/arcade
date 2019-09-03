// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public class PublishArtifactsInManifestTests
    {
        const string RandomToken = "abcd";
        const string BlobFeedUrl = "https://dotnetfeed.blob.core.windows.net/dotnet-core/index.json";

        [Fact]
        public async Task FeedConfigParserTests1Async()
        {
            var buildEngine = new MockBuildEngine();
            var task = new PublishArtifactsInManifest
            {
                // Create a single Microsoft.Build.Utilities.TaskItem for a simple feed config, then parse to FeedConfigs and
                // check the expected values.
                TargetFeedConfig = new Microsoft.Build.Utilities.TaskItem[]
                {
                    new Microsoft.Build.Utilities.TaskItem("FOOPACKAGES", new Dictionary<string, string> {
                        { "TargetUrl", BlobFeedUrl },
                        { "Token", RandomToken },
                        { "Type", "AzDoNugetFeed" },
                        { "Internal", "false" }}),
                },
                BuildEngine = buildEngine
            };

            await task.ParseTargetFeedConfigAsync();
            Assert.False(task.Log.HasLoggedErrors);

            // This will have set the feed configs.
            Assert.Collection(task.FeedConfigs,
                configList =>
                {
                    Assert.Equal("FOOPACKAGES", configList.Key);
                    Assert.Collection(configList.Value, config =>
                    {
                        Assert.Equal(RandomToken, config.Token);
                        Assert.Equal(BlobFeedUrl, config.TargetURL);
                        Assert.False(config.Internal);
                        Assert.Equal(FeedType.AzDoNugetFeed, config.Type);
                        Assert.Equal(AssetSelection.All, config.AssetSelection);
                    });
                });
        }

        [Fact]
        public async Task FeedConfigParserTests2Async()
        {
            var buildEngine = new MockBuildEngine();
            var task = new PublishArtifactsInManifest
            {
                TargetFeedConfig = new Microsoft.Build.Utilities.TaskItem[]
                {
                    new Microsoft.Build.Utilities.TaskItem("FOOPACKAGES", new Dictionary<string, string> {
                        { "TargetUrl", BlobFeedUrl },
                        { "Token", RandomToken },
                        { "Type", "MyUnknownFeedType" },
                        { "Internal", "false" } }),
                },
                BuildEngine = buildEngine
            };

            await task.ParseTargetFeedConfigAsync();
            Assert.True(task.Log.HasLoggedErrors);
            Assert.Contains(buildEngine.BuildErrorEvents, e => e.Message.Equals("Invalid feed config type 'MyUnknownFeedType'. Possible values are: AzDoNugetFeed, AzureStorageFeed"));
        }

        [Fact]
        public async Task FeedConfigParserTests3Async()
        {
            var buildEngine = new MockBuildEngine();
            var task = new PublishArtifactsInManifest
            {
                TargetFeedConfig = new Microsoft.Build.Utilities.TaskItem[]
                {
                    new Microsoft.Build.Utilities.TaskItem("FOOPACKAGES", new Dictionary<string, string> {
                        { "TargetUrl", string.Empty },
                        { "Token", string.Empty },
                        { "Type", string.Empty },
                        { "Internal", "false" } }),
                },
                BuildEngine = buildEngine
            };

            await task.ParseTargetFeedConfigAsync();
            Assert.True(task.Log.HasLoggedErrors);
            Assert.Contains(buildEngine.BuildErrorEvents, e => e.Message.Equals("Invalid FeedConfig entry. TargetURL='' Type='' Token=''"));
        }

        /// <summary>
        ///     Valid feed config with an asset selection set.
        /// </summary>
        [Fact]
        public async Task FeedConfigParserTests4Async()
        {
            var buildEngine = new MockBuildEngine();
            var task = new PublishArtifactsInManifest
            {
                TargetFeedConfig = new Microsoft.Build.Utilities.TaskItem[]
                {
                    new Microsoft.Build.Utilities.TaskItem("FOOPACKAGES", new Dictionary<string, string> {
                        { "TargetUrl", BlobFeedUrl },
                        { "Token", RandomToken },
                        // Use different casing here to make sure that parsing
                        // ignores case.
                        { "Type", "AZURESTORAGEFEED" },
                        { "AssetSelection", "SHIPPINGONLY" },
                        { "Internal", "false" }}),
                },
                BuildEngine = buildEngine
            };

            await task.ParseTargetFeedConfigAsync();

            // This will have set the feed configs.
            Assert.Collection(task.FeedConfigs,
                configList =>
                {
                    Assert.Equal("FOOPACKAGES", configList.Key);
                    Assert.Collection(configList.Value, config =>
                    {
                        Assert.Equal(RandomToken, config.Token);
                        Assert.Equal(BlobFeedUrl, config.TargetURL);
                        Assert.Equal(FeedType.AzureStorageFeed, config.Type);
                        Assert.Equal(AssetSelection.ShippingOnly, config.AssetSelection);
                    });
                });
        }

        /// <summary>
        ///     Check that internal builds don't publish to public feeds.
        /// </summary>
        [Fact]
        public async Task FeedConfigParserTests5Async()
        {
            var buildEngine = new MockBuildEngine();
            var task = new PublishArtifactsInManifest
            {
                InternalBuild = true,
                TargetFeedConfig = new Microsoft.Build.Utilities.TaskItem[]
                {
                    new Microsoft.Build.Utilities.TaskItem("FOOPACKAGES", new Dictionary<string, string> {
                        { "TargetUrl", BlobFeedUrl },
                        { "Token", RandomToken },
                        { "Type", "AZURESTORAGEFEED" },
                        { "AssetSelection", "SHIPPINGONLY" },
                        { "Internal", "true" }}),
                    new Microsoft.Build.Utilities.TaskItem("FOOPACKAGES", new Dictionary<string, string> {
                        { "TargetUrl", BlobFeedUrl },
                        { "Token", RandomToken },
                        { "Type", "AZURESTORAGEFEED" },
                        { "AssetSelection", "SHIPPINGONLY" } }),
                },
                BuildEngine = buildEngine
            };

            await task.ParseTargetFeedConfigAsync();
            // Verify that the checker errors on attempts to publish internal
            // artifacts to non-internal feeds
            Assert.True(task.Log.HasLoggedErrors);
            Assert.Contains(buildEngine.BuildErrorEvents, e => e.Message.Equals($"Use of non-internal feed '{BlobFeedUrl}' is invalid for an internal build. This can be overridden with '{nameof(PublishArtifactsInManifest.SkipSafetyChecks)}= true'"));
        }

        [Fact]
        public async Task FeedConfigParserTests6Async()
        {
            var buildEngine = new MockBuildEngine();
            var task = new PublishArtifactsInManifest
            {
                InternalBuild = true,
                TargetFeedConfig = new Microsoft.Build.Utilities.TaskItem[]
                {
                    new Microsoft.Build.Utilities.TaskItem("FOOPACKAGES", new Dictionary<string, string> {
                        { "TargetUrl", BlobFeedUrl },
                        { "Token", RandomToken },
                        { "Type", "AZURESTORAGEFEED" },
                        { "AssetSelection", "SHIPPINGONLY" },
                        { "Internal", "true" }}),
                    new Microsoft.Build.Utilities.TaskItem("FOOPACKAGES", new Dictionary<string, string> {
                        { "TargetUrl", BlobFeedUrl },
                        { "Token", RandomToken },
                        { "Type", "AZURESTORAGEFEED" },
                        { "AssetSelection", "SHIPPINGONLY" },
                        { "Internal", "true"} }),
                },
                BuildEngine = buildEngine
            };

            await task.ParseTargetFeedConfigAsync();
            Assert.True(!task.Log.HasLoggedErrors);
        }


        /// <summary>
        ///     Check that attempts to publish stable artifacts to non-stable feeds will throw errors.
        /// </summary>
        [Theory]
        [InlineData("3.0.0", false, true)]
        [InlineData("3.0.0-preview1", false, false)]
        [InlineData("3.0.0.10", false, true)]
        [InlineData("3.0.0-preview1-12345", false, false)]
        [InlineData("5.3.0-rtm.6198", false, false)]
        [InlineData("3.3.1-beta3-19430-03", false, false)]
        [InlineData("3.0.0", true, false)]
        [InlineData("3.0.0-preview1", true, false)]
        [InlineData("3.0.0.10", true, false)]
        [InlineData("3.0.0-preview1-12345", true, false)]
        [InlineData("5.3.0-rtm.6198", true, false)]
        [InlineData("3.3.1-beta3-19430-03", true, false)]
        [InlineData("3.0.0", false, false, true)]
        [InlineData("3.0.0.10", false, false, true)]
        public async Task StableAssetCheck1Async(string assetVersion, bool isIsolatedFeed, bool shouldError, bool skipChecks = false)
        {
            var buildEngine = new MockBuildEngine();
            var task = new PublishArtifactsInManifest
            {
                SkipSafetyChecks = skipChecks,
                TargetFeedConfig = new Microsoft.Build.Utilities.TaskItem[]
                {
                    new Microsoft.Build.Utilities.TaskItem("NETCORE", new Dictionary<string, string> {
                        { "TargetUrl", BlobFeedUrl },
                        { "Token", RandomToken },
                        { "Type", "AZURESTORAGEFEED" },
                        { "AssetSelection", "SHIPPINGONLY" },
                        { "Internal", "false" },
                        // Feed is not isolated
                        { "Isolated", isIsolatedFeed.ToString() }})
                },
                BuildEngine = buildEngine
            };

            const string packageId = "Foo.Package";

            BuildModel buildModel = new BuildModel(new BuildIdentity())
            {
                Artifacts = new ArtifactSet
                {
                    Blobs = new List<BlobArtifactModel>(),
                    Packages = new List<PackageArtifactModel>
                    {
                        new PackageArtifactModel()
                        {
                            Id = packageId,
                            Version = assetVersion
                        }
                    }
                }
            };

            await task.ParseTargetFeedConfigAsync();
            Assert.False(task.Log.HasLoggedErrors);

            task.SplitArtifactsInCategories(buildModel);
            Assert.False(task.Log.HasLoggedErrors);

            task.CheckForStableAssets();
            Assert.Equal(shouldError, task.Log.HasLoggedErrors);
            if (shouldError)
            {
                Assert.Contains(buildEngine.BuildErrorEvents, e => e.Message.Equals($"Package '{packageId}' has stable version '{assetVersion}' but is targeted at a non-isolated feed '{BlobFeedUrl}'"));
            }
        }

        [Theory]
        [InlineData("https://pkgs.dev.azure.com/dnceng/public/_packaging/mmitche-test-transport/nuget/v3/index.json", "dnceng", "public", "mmitche-test-transport")]
        [InlineData("https://pkgs.dev.azure.com/DevDiv/public/_packaging/1234.5/nuget/v3/index.json", "DevDiv", "public", "1234.5")]
        public void NugetFeedParseTests(string uri, string account, string project, string feed)
        {
            var matches = Regex.Match(uri, PublishArtifactsInManifest.AzDoNuGetFeedPattern);
            Assert.Equal(account, matches.Groups["account"]?.Value);
            Assert.Equal(project, matches.Groups["project"]?.Value);
            Assert.Equal(feed, matches.Groups["feed"]?.Value);
        }

        [Theory]
        // Simple case where we fill the whole buffer on each stream call and the streams match
        [InlineData("QXJjYWRl", "QXJjYWRl", new int[] { int.MaxValue }, new int[] { int.MaxValue }, 1024)]
        // Simple case where we fill the whole buffer on each stream call and the streams don't match
        [InlineData("QXJjYWRl", "QXJjYWRm", new int[] { int.MaxValue }, new int[] { int.MaxValue }, 1024)]
        // Case where the first stream returns everything initially, but the second returns one byte at a time.
        [InlineData("QXJjYWRl", "QXJjYWRl", new int[] { int.MaxValue }, new int[] { 1, 1, 1, 1, 1 }, 1024)]
        // Case where the first stream returns everything initially, but the second returns one byte at a time and they are not equal
        [InlineData("QXJjYWRl", "QXJjYWRm", new int[] { int.MaxValue }, new int[] { 1, 1, 1, 1, 1 }, 1024)]
        // Case where both streams return one byte at a time
        [InlineData("QXJjYWRl", "QXJjYWRl", new int[] { 1, 1, 1, 1, 1 }, new int[] { 1, 1, 1, 1, 1 }, 1024)]
        // Case where both streams return one byte at a time
        [InlineData("QXJjYWRl", "QXJjYWQ=", new int[] { 1, 1, 1, 1, 1 }, new int[] { 1, 1, 1, 1, 1 }, 1024)]
        // Case where the buffer must wrap around and one stream returns faster than the other, equal streams
        [InlineData("VGhlIHF1aWNrIGJyb3JuIGZveCBqdW1wcyBvdmVyIHRoZSBsYXp5aXNoIGRvZ2dv", "VGhlIHF1aWNrIGJyb3JuIGZveCBqdW1wcyBvdmVyIHRoZSBsYXp5aXNoIGRvZ2dv", new int[] { 16, 16, 16, 16, 16, 16 }, new int[] { 1, 1, 1, 1, 1 }, 8)]
        // Case where the buffer must wrap around and one stream returns faster than the other, unequal streams
        [InlineData("VGhpcyBpcyBhIHNlbnRlbmNlIHRoYXQgaXMgYSBsaXR0bGUgbG9uZ2Vy", "VGhpcyBpcyBhIHNlbnRlbmNlIHRoYXQgaXMgYSBsb25nZXI=", new int[] { 7, 3, 5, 16, 16, 16 }, new int[] { 1, 1, 1, 1, 1 }, 8)]
        public async Task StreamComparisonTestsAsync(string streamA, string streamB, int[] maxStreamABytesReturnedEachCall, int[] maxStreamBBytesReturnedEachCall, int bufferSize)
        {
            byte[] streamABytes = Convert.FromBase64String(streamA);
            byte[] streamBBytes = Convert.FromBase64String(streamB);

            FakeStream fakeStreamA = new FakeStream(streamABytes, maxStreamABytesReturnedEachCall);
            FakeStream fakeStreamB = new FakeStream(streamBBytes, maxStreamBBytesReturnedEachCall);

            Assert.Equal(streamA == streamB, await PublishArtifactsInManifest.CompareStreamsAsync(fakeStreamA, fakeStreamB, bufferSize));
        }

        class FakeStream : Stream
        {
            public FakeStream(byte[] streamBytes, int[] maxStreamBytesReturned)
            {
                _streamBytes = streamBytes;
                _maxStreamBytesReturned = maxStreamBytesReturned;
            }

            byte[] _streamBytes;
            int[] _maxStreamBytesReturned;
            int _callIndex = 0;
            int _position = 0;

            public override System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Assert.True(count > 0);

                // If we reach the end of the _maxStreamBytesReturned array, just use max int.
                int maxStreamBytesThisCall = int.MaxValue;
                if (_callIndex < _maxStreamBytesReturned.Length)
                {
                    maxStreamBytesThisCall = _maxStreamBytesReturned[_callIndex];
                    _callIndex++;
                }
                int bytesToWrite = Math.Min(Math.Min(_streamBytes.Length - _position, count), maxStreamBytesThisCall);

                for (int i = 0; i < bytesToWrite; i++)
                {
                    buffer[offset + i] = _streamBytes[_position + i];
                }
                _position += bytesToWrite;

                return System.Threading.Tasks.Task.FromResult(bytesToWrite);
            }

            #region Unused

            public override bool CanRead => throw new NotImplementedException();

            public override bool CanSeek => throw new NotImplementedException();

            public override bool CanWrite => throw new NotImplementedException();

            public override long Length => throw new NotImplementedException();

            public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }
            #endregion
        }
    }
}
