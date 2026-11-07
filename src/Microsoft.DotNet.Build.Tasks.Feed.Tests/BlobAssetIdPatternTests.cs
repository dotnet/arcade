// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

public class BlobAssetIdPatternTests
{
    private const string DailyId = "dotnetup/0.2.0-daily.1.12345.1/dotnetup-win-x64.exe";
    private const string PreviewId = "dotnetup/0.2.0-preview.1.12345.1/dotnetup-win-x64.exe";
    private const string SharedId = "assets/manifests/repo/build/MergedManifest.xml";
    private const string DailyPattern = "^dotnetup/[^/]+-daily[.]";
    private const string PreviewPattern = "^dotnetup/[^/]+-preview[.]";
    private const string SharedPattern = "|^assets/manifests/";

    private sealed class RecordingPublisher : PublishArtifactsInManifestBase
    {
        public int ExecutionCount { get; private set; }
        public string[] PublishedIds { get; private set; }

        public RecordingPublisher(params string[] ids)
        {
            BuildModel = new BuildModel(new BuildIdentity { Name = "repo" })
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
        public bool LoadManifests { get; set; }

        public RecordingTask(params RecordingPublisher[] publishers)
        {
            _publishers = new Queue<RecordingPublisher>(publishers);
            BuildEngine = new MockBuildEngine();
            AssetManifestPaths = publishers.Select((_, index) => (ITaskItem)new TaskItem($"manifest-{index}.xml")).ToArray();
        }

        public override PublishArtifactsInManifestBase WhichPublishingTask(string manifestFullPath)
        {
            var publisher = _publishers.Dequeue();
            if (LoadManifests)
            {
                publisher.BuildModel = base.WhichPublishingTask(manifestFullPath).BuildModel;
            }
            return publisher;
        }

        protected override Task PromoteBuildToChannelsAsync()
        {
            PromotionCount++;
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void OmittedOrEmptyPropertyPreservesAllBlobs(string pattern)
    {
        var publisher = new RecordingPublisher(DailyId, PreviewId, SharedId);
        var task = new RecordingTask(publisher) { BlobAssetIdPattern = pattern };
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        var originalBlobs = publisher.BuildModel.Artifacts.Blobs;

        task.ExecuteTask(null, fileSystem.Object, null).Should().BeTrue();

        publisher.PublishedIds.Should().Equal(DailyId, PreviewId, SharedId);
        publisher.BuildModel.Artifacts.Blobs.Should().BeSameAs(originalBlobs);
        task.PromotionCount.Should().Be(1);
        fileSystem.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(DailyPattern, DailyId, false)]
    [InlineData(DailyPattern, DailyId, true)]
    [InlineData(PreviewPattern, PreviewId, false)]
    [InlineData(PreviewPattern, PreviewId, true)]
    public void SelectsCandidateAndOnlyExplicitlyMatchedSharedBlobs(string pattern, string candidateId, bool includeShared)
    {
        var publisher = new RecordingPublisher(DailyId, PreviewId, SharedId, "unrelated/dotnetup-win-x64.exe");
        var task = new RecordingTask(publisher) { BlobAssetIdPattern = pattern + (includeShared ? SharedPattern : "") };
        BlobArtifactModel candidate = publisher.BuildModel.Artifacts.Blobs.Single(blob => blob.Id == candidateId);
        candidate.NonShipping = true;
        var packages = publisher.BuildModel.Artifacts.Packages;
        var pdbs = publisher.BuildModel.Artifacts.Pdbs;

        task.ExecuteTask(null, null, null).Should().BeTrue();

        publisher.PublishedIds.Should().Equal(includeShared ? [candidateId, SharedId] : [candidateId]);
        publisher.BuildModel.Artifacts.Blobs.Should().Contain(candidate);
        candidate.NonShipping.Should().BeTrue();
        publisher.BuildModel.Artifacts.Packages.Should().BeSameAs(packages);
        publisher.BuildModel.Artifacts.Pdbs.Should().BeSameAs(pdbs);
        task.PromotionCount.Should().Be(1);
    }

    [Fact]
    public void SelectsAcrossAllManifestsAndAllowsIndividualManifestsWithoutMatches()
    {
        var daily = new RecordingPublisher(DailyId);
        var preview = new RecordingPublisher(PreviewId);
        var shared = new RecordingPublisher(SharedId);
        var task = new RecordingTask(daily, preview, shared) { BlobAssetIdPattern = DailyPattern + SharedPattern };

        task.ExecuteTask(null, null, null).Should().BeTrue();

        daily.PublishedIds.Should().Equal(DailyId);
        preview.PublishedIds.Should().BeEmpty();
        shared.PublishedIds.Should().Equal(SharedId);
        foreach (var publisher in new[] { daily, preview, shared })
        {
            publisher.ExecutionCount.Should().Be(1);
            publisher.BuildModel.Artifacts.Packages.Should().ContainSingle();
            publisher.BuildModel.Artifacts.Pdbs.Should().ContainSingle();
        }
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
            BlobAssetIdPattern = $"^dotnetup/[^/]+-{quality}[.]" + SharedPattern
        };

        task.ExecuteTask(null, null, null).Should().BeTrue();

        publisher.PublishedIds.Should().Equal([.. candidateIds, SharedId]);
        publisher.PublishedIds.Should().NotIntersectWith(otherIds);
        task.PromotionCount.Should().Be(1);
    }

    [Theory]
    [InlineData("[", "Invalid BlobAssetIdPattern")]
    [InlineData("^unknown[.]zip$", "did not match any blobs")]
    [InlineData("^DOTNETUP/", "did not match any blobs")]
    [InlineData(" ", "did not match any blobs")]
    public void InvalidOrEmptySelectionDoesNotMutatePublishOrPromote(string pattern, string error)
    {
        var daily = new RecordingPublisher(DailyId, SharedId);
        var preview = new RecordingPublisher(PreviewId);
        AssertSelectionFailure(pattern, error, daily, preview);
    }

    [Fact]
    public void NoBlobsAcrossManifestsFailsBeforePublishingPackagesOrPromoting()
    {
        AssertSelectionFailure(".*", "did not match any blobs", new RecordingPublisher(), new RecordingPublisher());
    }

    [Fact]
    public void TimeoutInLaterManifestDoesNotApplyEarlierSelectionOrPublishOrPromote()
    {
        var first = new RecordingPublisher("aaaa", PreviewId);
        var second = new RecordingPublisher(new string('a', 10000) + "!");
        AssertSelectionFailure("^a+$|^(a+)+$", "exceeded the regex match timeout", first, second);
    }

    private static void AssertSelectionFailure(string pattern, string error, params RecordingPublisher[] publishers)
    {
        var originalBlobs = publishers.Select(p => p.BuildModel.Artifacts.Blobs).ToArray();
        var originalIds = originalBlobs.Select(blobs => blobs.Select(b => b.Id).ToArray()).ToArray();
        var packages = publishers.Select(p => p.BuildModel.Artifacts.Packages).ToArray();
        var pdbs = publishers.Select(p => p.BuildModel.Artifacts.Pdbs).ToArray();
        var task = new RecordingTask(publishers) { BlobAssetIdPattern = pattern };

        task.ExecuteTask(null, null, null).Should().BeFalse();

        for (int i = 0; i < publishers.Length; i++)
        {
            publishers[i].ExecutionCount.Should().Be(0);
            publishers[i].BuildModel.Artifacts.Blobs.Should().BeSameAs(originalBlobs[i]);
            publishers[i].BuildModel.Artifacts.Blobs.Select(b => b.Id).Should().Equal(originalIds[i]);
            publishers[i].BuildModel.Artifacts.Packages.Should().BeSameAs(packages[i]);
            publishers[i].BuildModel.Artifacts.Pdbs.Should().BeSameAs(pdbs[i]);
        }
        task.PromotionCount.Should().Be(0);
        ((MockBuildEngine)task.BuildEngine).BuildErrorEvents.Should().Contain(e => e.Message.Contains(error));
    }

    [Fact]
    public void MatchingIsCaseSensitiveAgainstFullId()
    {
        var publisher = new RecordingPublisher(DailyId, DailyId.ToUpperInvariant(), "dotnetup-win-x64.exe");
        var task = new RecordingTask(publisher) { BlobAssetIdPattern = DailyPattern };

        task.ExecuteTask(null, null, null).Should().BeTrue();

        publisher.PublishedIds.Should().Equal(DailyId);
    }

    [Fact]
    public void InlineCaseInsensitiveMatchingIsCultureInvariant()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var publisher = new RecordingPublisher("I", "\u0130", "\u0131");
            var task = new RecordingTask(publisher) { BlobAssetIdPattern = "(?i)^i$" };

            task.ExecuteTask(null, null, null).Should().BeTrue();

            publisher.PublishedIds.Should().Equal("I");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(PublishingInfraVersion.V3)]
    [InlineData(PublishingInfraVersion.V4)]
    public void SelectionPreservesOriginalManifestOnDisk(PublishingInfraVersion version)
    {
        string directory = Path.Combine(Directory.GetCurrentDirectory(), $"blob-pattern-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "MergedManifest.xml");
            var publisher = new RecordingPublisher(DailyId, PreviewId, SharedId);
            publisher.BuildModel.Identity.PublishingVersion = version;
            File.WriteAllText(path, publisher.BuildModel.ToXml().ToString());
            byte[] originalContents = File.ReadAllBytes(path);
            var task = new RecordingTask(publisher)
            {
                BlobAssetIdPattern = DailyPattern,
                LoadManifests = true,
                AssetManifestPaths = [new TaskItem(path)]
            };
            var fileSystem = new FileSystem();
            var factory = new BuildModelFactory(null, null, null, fileSystem, task.Log);

            task.ExecuteTask(factory, fileSystem, null).Should().BeTrue();

            publisher.PublishedIds.Should().Equal(DailyId);
            File.ReadAllBytes(path).Should().Equal(originalContents);
            factory.ManifestFileToModel(path).Artifacts.Blobs.Should().HaveCount(3);
            task.PromotionCount.Should().Be(1);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
