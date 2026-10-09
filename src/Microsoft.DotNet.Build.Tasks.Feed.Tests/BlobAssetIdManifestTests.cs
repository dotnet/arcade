// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Arcade.Common;
using Microsoft.Arcade.Test.Common;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Manifest;
using Moq;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests;

public class BlobAssetIdManifestTests
{
    private const string DailyId = "dotnetup/0.2.0-daily.1.12345.1/dotnetup-win-x64.exe";
    private const string PreviewId = "dotnetup/0.2.0-preview.1.12345.1/dotnetup-win-x64.exe";
    private const string SharedId = "assets/manifests/repo/build/MergedManifest.xml";
    private static readonly string BlobBasePath = Path.Combine(Path.GetTempPath(), "blob-assets");

    private sealed class RecordingPublisher : PublishArtifactsInManifestBase
    {
        public int ExecutionCount { get; private set; }
        public string[] PublishedIds { get; private set; }

        public RecordingPublisher(params string[] ids)
        {
            BuildModel = new BuildModel(new BuildIdentity())
            {
                Artifacts = new ArtifactSet
                {
                    Blobs = ids.Select(id => new BlobArtifactModel { Id = id }).ToList(),
                    Packages = [new PackageArtifactModel { Id = "package", Version = "1.0.0" }],
                    Pdbs = [new PdbArtifactModel { Id = "symbols.pdb" }]
                }
            };
        }

        public override Task<bool> ExecuteAsync()
        {
            ExecutionCount++;
            PublishedIds = BuildModel.Artifacts.Blobs.Select(blob => blob.Id).ToArray();
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingTask : PublishArtifactsInManifest
    {
        private readonly Queue<RecordingPublisher> _publishers;
        public int PromotionCount { get; private set; }

        public RecordingTask(params RecordingPublisher[] publishers)
        {
            _publishers = new Queue<RecordingPublisher>(publishers);
            BuildEngine = new MockBuildEngine();
            BlobAssetsBasePath = BlobBasePath;
            AssetManifestPaths = publishers.Select((_, index) => (ITaskItem)new TaskItem($"manifest-{index}.xml")).ToArray();
        }

        public override PublishArtifactsInManifestBase WhichPublishingTask(string manifestFullPath) => _publishers.Dequeue();

        protected override Task PromoteBuildToChannelsAsync()
        {
            PromotionCount++;
            return Task.CompletedTask;
        }
    }

    private static Mock<IFileSystem> CreateFileSystem(params (string Path, string Content)[] files)
    {
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        foreach (var (path, content) in files)
        {
            fileSystem.Setup(fs => fs.FileExists(path)).Returns(true);
            fileSystem.Setup(fs => fs.GetFileStream(path, FileMode.Open, FileAccess.Read))
                .Returns(() => new MemoryStream(Encoding.UTF8.GetBytes(content)));
        }
        return fileSystem;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OmittedOrEmptyPropertyPreservesAllBlobs(bool empty)
    {
        var publisher = new RecordingPublisher(DailyId, PreviewId, SharedId);
        var task = new RecordingTask(publisher)
        {
            BlobAssetIdManifests = empty ? [] : null
        };
        var fileSystem = CreateFileSystem();

        task.ExecuteTask(null, fileSystem.Object, null).Should().BeTrue();

        publisher.PublishedIds.Should().Equal(DailyId, PreviewId, SharedId);
        task.PromotionCount.Should().Be(1);
        fileSystem.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(DailyId)]
    [InlineData(PreviewId)]
    public void SelectsOnlyExactIdsBeforePublishing(string candidateId)
    {
        var publisher = new RecordingPublisher(DailyId, PreviewId, SharedId);
        var task = new RecordingTask(publisher)
        {
            BlobAssetIdManifests = [new TaskItem("candidate.blobids")]
        };
        var fileSystem = CreateFileSystem((Path.Combine(BlobBasePath, "candidate.blobids"),
            $"\n {candidateId} \r\n{SharedId}\n{candidateId}\n"));
        BlobArtifactModel candidate = publisher.BuildModel.Artifacts.Blobs.Single(blob => blob.Id == candidateId);
        candidate.NonShipping = true;

        task.ExecuteTask(null, fileSystem.Object, null).Should().BeTrue();

        publisher.PublishedIds.Should().Equal(candidateId, SharedId);
        publisher.BuildModel.Artifacts.Blobs.Should().Contain(candidate);
        candidate.NonShipping.Should().BeTrue();
        publisher.BuildModel.Artifacts.Packages.Should().ContainSingle();
        publisher.BuildModel.Artifacts.Pdbs.Should().ContainSingle();
        task.PromotionCount.Should().Be(1);
    }

    [Fact]
    public void CombinesFilesAndValidatesAcrossAllBuildManifests()
    {
        var daily = new RecordingPublisher(DailyId);
        var preview = new RecordingPublisher(PreviewId);
        var shared = new RecordingPublisher(SharedId);
        string absolutePath = Path.Combine(Path.GetTempPath(), "shared.blobids");
        var task = new RecordingTask(daily, preview, shared)
        {
            BlobAssetIdManifests = [new TaskItem("daily.blobids"), new TaskItem(absolutePath)]
        };
        var fileSystem = CreateFileSystem(
            (Path.Combine(BlobBasePath, "daily.blobids"), DailyId),
            (absolutePath, $"{SharedId}\n{DailyId}"));

        task.ExecuteTask(null, fileSystem.Object, null).Should().BeTrue();

        daily.PublishedIds.Should().Equal(DailyId);
        preview.PublishedIds.Should().BeEmpty();
        shared.PublishedIds.Should().Equal(SharedId);
        task.PromotionCount.Should().Be(1);
    }

    [Theory]
    [InlineData("daily", "preview")]
    [InlineData("preview", "daily")]
    public void SelectsCompleteCandidateFromCombinedInventory(string quality, string otherQuality)
    {
        string[] candidateIds = Enumerable.Range(0, 20)
            .Select(index => $"dotnetup/0.2.0-{quality}.1.12345.1/asset-{index}").ToArray();
        string[] otherIds = Enumerable.Range(0, 20)
            .Select(index => $"dotnetup/0.2.0-{otherQuality}.1.12345.1/asset-{index}").ToArray();
        var publisher = new RecordingPublisher([.. candidateIds, .. otherIds, SharedId]);
        var task = new RecordingTask(publisher)
        {
            BlobAssetIdManifests = [new TaskItem("candidate.blobids")]
        };
        var fileSystem = CreateFileSystem((Path.Combine(BlobBasePath, "candidate.blobids"),
            string.Join(Environment.NewLine, [.. candidateIds, SharedId])));

        task.ExecuteTask(null, fileSystem.Object, null).Should().BeTrue();

        publisher.PublishedIds.Should().HaveCount(21);
        publisher.PublishedIds.Should().Equal([.. candidateIds, SharedId]);
        publisher.PublishedIds.Should().NotIntersectWith(otherIds);
        task.PromotionCount.Should().Be(1);
    }

    [Theory]
    [InlineData("", "do not select any blobs")]
    [InlineData(" \r\n\t\n", "do not select any blobs")]
    [InlineData("unknown.zip", "was not found")]
    [InlineData("DOTNETUP/0.2.0-daily.1.12345.1/dotnetup-win-x64.exe", "was not found")]
    [InlineData(DailyId + "\nunknown.zip", "was not found")]
    public void InvalidSelectionDoesNotPublishOrPromote(string contents, string error)
    {
        var publisher = new RecordingPublisher(DailyId, PreviewId, SharedId);
        var task = new RecordingTask(publisher)
        {
            BlobAssetIdManifests = [new TaskItem("candidate.blobids")]
        };
        var fileSystem = CreateFileSystem((Path.Combine(BlobBasePath, "candidate.blobids"), contents));

        task.ExecuteTask(null, fileSystem.Object, null).Should().BeFalse();

        publisher.ExecutionCount.Should().Be(0);
        task.PromotionCount.Should().Be(0);
        publisher.BuildModel.Artifacts.Blobs.Should().HaveCount(3);
        ((MockBuildEngine)task.BuildEngine).BuildErrorEvents.Should().Contain(e => e.Message.Contains(error));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrUnreadableFileDoesNotPublishOrPromote(bool unreadable)
    {
        var publisher = new RecordingPublisher(DailyId, PreviewId);
        string path = Path.Combine(BlobBasePath, "candidate.blobids");
        var task = new RecordingTask(publisher)
        {
            BlobAssetIdManifests = [new TaskItem("candidate.blobids")]
        };
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(fs => fs.FileExists(path)).Returns(unreadable);
        if (unreadable)
        {
            fileSystem.Setup(fs => fs.GetFileStream(path, FileMode.Open, FileAccess.Read))
                .Throws(new IOException("Cannot read candidate allowlist."));
        }

        task.ExecuteTask(null, fileSystem.Object, null).Should().BeFalse();

        publisher.ExecutionCount.Should().Be(0);
        task.PromotionCount.Should().Be(0);
        ((MockBuildEngine)task.BuildEngine).BuildErrorEvents.Should().Contain(e =>
            e.Message.Contains(unreadable ? "Cannot read candidate allowlist" : "does not exist"));
    }
}
