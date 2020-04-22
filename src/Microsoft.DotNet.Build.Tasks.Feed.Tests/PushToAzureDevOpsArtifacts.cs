// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public class PushToAzureDevOpsArtifactsTests
    {
        [Fact]
        public void HasRecordedPublishingVersion()
        {
            var targetManifiestPath = $"{Path.GetTempPath()}TestManifest-{Guid.NewGuid()}.xml";
            var buildId = "1.2.3";
            var initialAssetsLocation = "cloud";
            var isStable = false;
            var expectedManifestContent = $"<Build PublishingVersion=\"{BuildManifestUtil.LatestPublishingInfraVersion}\" BuildId=\"{buildId}\" InitialAssetsLocation=\"{initialAssetsLocation}\" IsStable=\"{isStable}\" />";

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                BuildEngine = buildEngine,
                ItemsToPush = new Microsoft.Build.Utilities.TaskItem[0],
                IsStableBuild = isStable,
                ManifestBuildId = buildId,
                ManifestBuildData = new string[] { $"InitialAssetsLocation={initialAssetsLocation}" },
                AssetManifestPath = targetManifiestPath
            };

            task.Execute();

            Assert.Equal(expectedManifestContent, File.ReadAllText(targetManifiestPath));
        }

        [Fact]
        public void UsesCustomPublishingVersion()
        {
            var targetManifiestPath = $"{Path.GetTempPath()}TestManifest-{Guid.NewGuid()}.xml";
            var buildId = "1.2.3";
            var initialAssetsLocation = "cloud";
            var isStable = false;
            var publishingInfraVersion = "456";
            var expectedManifestContent = $"<Build PublishingVersion=\"{publishingInfraVersion}\" BuildId=\"{buildId}\" InitialAssetsLocation=\"{initialAssetsLocation}\" IsStable=\"{isStable}\" />";

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                BuildEngine = buildEngine,
                ItemsToPush = new Microsoft.Build.Utilities.TaskItem[0],
                IsStableBuild = isStable,
                ManifestBuildId = buildId,
                ManifestBuildData = new string[] { $"InitialAssetsLocation={initialAssetsLocation}" },
                PublishingVersion = publishingInfraVersion,
                AssetManifestPath = targetManifiestPath
            };

            task.Execute();

            Assert.Equal(expectedManifestContent, File.ReadAllText(targetManifiestPath));
        }
    }
}