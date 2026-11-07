// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using AwesomeAssertions;
using Microsoft.DotNet.Build.Manifest;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests;

public class PublishBuildToMaestroTests
{
    [Theory]
    [InlineData("", "assets/manifests/sdk/20261013.1/MergedManifest.xml", "AssetManifests")]
    [InlineData("daily", "assets/manifests/sdk/20261013.1/variants/daily/MergedManifest.xml", "AssetManifests_daily")]
    [InlineData("preview", "assets/manifests/sdk/20261013.1/variants/preview/MergedManifest.xml", "AssetManifests_preview")]
    public void ArchivedManifestIdentifiesBuildVariant(string variant, string expectedId, string artifactName)
    {
        var model = new BuildModel(new BuildIdentity
        {
            Name = "sdk",
            AzureDevOpsBuildNumber = "20261013.1",
            PublishingVersion = PublishingInfraVersion.V4,
        });
        var task = new PublishBuildToMaestro
        {
            BuildVariant = variant,
        };

        var manifest = task.AddManifestAsAsset(model, Path.Combine("staging", "MergedManifest.xml"));

        manifest.Id.Should().Be(expectedId);
        manifest.PipelineArtifactName.Should().Be(artifactName);
        manifest.PipelineArtifactPath.Should().Be("MergedManifest.xml");
        model.Artifacts.Blobs.Should().ContainSingle().Which.Should().BeSameAs(manifest);
    }

    [Fact]
    public void TwoVariantsRegisterDisjointAssetsWithTheSamePipelineIdentity()
    {
        var records = new[] { "daily", "preview" }.Select(variant =>
        {
            var model = new BuildModel(new BuildIdentity
            {
                Name = "sdk",
                Commit = "abcdef",
                AzureDevOpsBuildNumber = "20261013.1",
                AzureDevOpsBuildId = 123,
                AzureDevOpsBuildDefinitionId = 456,
                PublishingVersion = PublishingInfraVersion.V4,
            });
            model.Artifacts.Blobs.Add(new BlobArtifactModel
            {
                Id = $"dotnetup/0.3.0-{variant}.1.26505.1/dotnetup-win-x64.exe",
            });
            var task = new PublishBuildToMaestro { BuildVariant = variant };
            var archive = task.AddManifestAsAsset(model, "MergedManifest.xml");
            return task.GetMaestroBuildDataFromMergedManifest(model, archive, CancellationToken.None);
        }).ToArray();

        records[0].AzureDevOpsBuildId.Should().Be(records[1].AzureDevOpsBuildId);
        records[0].AzureDevOpsBuildDefinitionId.Should().Be(records[1].AzureDevOpsBuildDefinitionId);
        records[0].Commit.Should().Be(records[1].Commit);
        records[0].Assets.Select(a => a.Name).Intersect(records[1].Assets.Select(a => a.Name)).Should().BeEmpty();
        records[0].Assets.Should().Contain(a => a.Name.Contains("-daily."));
        records[1].Assets.Should().Contain(a => a.Name.Contains("-preview."));
    }

    [Fact]
    public void GetTimeToWait_ReadsGitHubRateLimitingHeaders()
    {
        // Unauthenticated requests to github that are rate limited don't use the Retry-After header,
        // so we need to be able to handle their X-RateLimit-Reset header.

        DateTime resetTime = DateTime.UtcNow.AddSeconds(5.0);
        long unixTime = (long)(resetTime - DateTime.UnixEpoch).TotalSeconds;
        var response = new System.Net.Http.HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Headers =
            {
                { "X-RateLimit-Remaining", "0" },
                { "X-RateLimit-Reset", unixTime.ToString() },
            }
        };

        TimeSpan? actual = PublishBuildToMaestro.GetTimeToWait(response);

        actual.Should().NotBeNull();
        actual.Should().BeCloseTo(resetTime - DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTimeToWait_NotWaitWhenNotLimited()
    {
        DateTime resetTime = DateTime.UtcNow.AddSeconds(5.0);
        long unixTime = (long)(resetTime - DateTime.UnixEpoch).TotalSeconds;
        var response = new System.Net.Http.HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Headers =
            {
                { "X-RateLimit-Remaining", "1" },
                { "X-RateLimit-Reset", unixTime.ToString() },
            }
        };

        TimeSpan? actual = PublishBuildToMaestro.GetTimeToWait(response);

        actual.Should().BeNull();
    }

    [Fact]
    public void GetTimeToWait_UsesRetryAfterDuration()
    {
        var response = new System.Net.Http.HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Headers =
            {
                { "Retry-After", "5" },
            }
        };
        TimeSpan? actual = PublishBuildToMaestro.GetTimeToWait(response);
        actual.Should().NotBeNull();
        actual.Should().BeCloseTo(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTimeToWait_UsesRetryAfterDate()
    {
        DateTime resetTime = DateTime.UtcNow.AddSeconds(5.0);
        var response = new System.Net.Http.HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Headers =
            {
                { "Retry-After", resetTime.ToString("R") },
            }
        };
        TimeSpan? actual = PublishBuildToMaestro.GetTimeToWait(response);
        actual.Should().NotBeNull();
        actual.Should().BeCloseTo(resetTime - DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
