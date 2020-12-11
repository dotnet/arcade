// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Build.Tasks.Feed.Tests.TestDoubles;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.IO;
using Xunit;
using FluentAssertions;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public class PushToAzureDevOpsArtifactsTests
    {
        [Fact]
        public void HasRecordedPublishingVersion()
        {
            var targetManifestPath = $"{Path.GetTempPath()}TestManifest-{Guid.NewGuid()}.xml";
            var buildId = "1.2.3";
            var initialAssetsLocation = "cloud";
            var isStable = false;
            var isReleaseOnlyPackageVersion = false;
            var expectedManifestContent = $"<Build PublishingVersion=\"{(int)PublishingInfraVersion.Latest}\" BuildId=\"{buildId}\" InitialAssetsLocation=\"{initialAssetsLocation}\" IsReleaseOnlyPackageVersion=\"{isReleaseOnlyPackageVersion.ToString().ToLower()}\" IsStable=\"{isStable.ToString().ToLower()}\" />";

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                BuildEngine = buildEngine,
                ItemsToPush = new Microsoft.Build.Utilities.TaskItem[0],
                IsStableBuild = isStable,
                ManifestBuildId = buildId,
                IsReleaseOnlyPackageVersion = isReleaseOnlyPackageVersion,
                ManifestBuildData = new string[] { $"InitialAssetsLocation={initialAssetsLocation}" },
                AssetManifestPath = targetManifestPath
            };

            task.Execute();

            var outputManifestContent = File.ReadAllText(targetManifestPath);
            outputManifestContent.Should().Be(expectedManifestContent);
        }

        [Fact]
        public void UsesCustomPublishingVersion()
        {
            var targetManifestPath = $"{Path.GetTempPath()}TestManifest-{Guid.NewGuid()}.xml";
            var buildId = "1.2.3";
            var initialAssetsLocation = "cloud";
            var isStable = false;
            var publishingInfraVersion = "456";
            var isReleaseOnlyPackageVersion = false;
            var expectedManifestContent = $"<Build PublishingVersion=\"{publishingInfraVersion}\" BuildId=\"{buildId}\" InitialAssetsLocation=\"{initialAssetsLocation}\" IsReleaseOnlyPackageVersion=\"{isReleaseOnlyPackageVersion.ToString().ToLower()}\" IsStable=\"{isStable.ToString().ToLower()}\" />";

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                BuildEngine = buildEngine,
                ItemsToPush = new Microsoft.Build.Utilities.TaskItem[0],
                IsStableBuild = isStable,
                IsReleaseOnlyPackageVersion = isReleaseOnlyPackageVersion,
                ManifestBuildId = buildId,
                ManifestBuildData = new string[] { $"InitialAssetsLocation={initialAssetsLocation}" },
                PublishingVersion = publishingInfraVersion,
                AssetManifestPath = targetManifestPath
            };

            task.Execute();

            var outputManifestContent = File.ReadAllText(targetManifestPath);
            outputManifestContent.Should().Be(expectedManifestContent);
        }
    }
}
