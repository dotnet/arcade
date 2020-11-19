// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Feed.Tests.TestDoubles;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
//using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    [TestFixture]
    public class PushToAzureDevOpsArtifactsTests
    {
        // We're using a fake file system so all of our paths can be fake, too
        const string TargetManifestPath = @"C:\manifests\TestManifest.xml";
        const string PackageA = @"C:\packages\test-package-a.nupkg";
        const string PackageB = @"C:\packages\test-package-b.nupkg";
        const string SampleManifest = @"C:\manifests\SampleManifest.xml";

        static TaskItem[] TaskItems = new TaskItem[]
        {
            new TaskItem(PackageA, new Dictionary<string, string>
            {
                { "RelativeBlobPath", PackageA },
                { "IsShipping", "false" },
                { "ManifestArtifactData", "Nonshipping=true" },
            }),
            new TaskItem(PackageB, new Dictionary<string, string>
            {
                { "RelativeBlobPath", PackageB },
                { "IsShipping", "true" },
                { "ManifestArtifactData", "Nonshipping=false" },
            }),
            new TaskItem(SampleManifest, new Dictionary<string, string>
            {
                { "IsShipping", "false" },
                { "RelativeBlobPath", SampleManifest },
                { "ManifestArtifactData", "Nonshipping=false" },
                { "PublishFlatContainer", "true" },
            }),
        };

        [Test]
        public void HasRecordedPublishingVersion()
        {
            var targetManifiestPath = $"{Path.GetTempPath()}TestManifest-{Guid.NewGuid()}.xml";
            var buildId = "1.2.3";
            var initialAssetsLocation = "cloud";
            var isStable = false;
            var isReleaseOnlyPackageVersion = false;
            var expectedManifestContent = $"<Build PublishingVersion=\"{(int)PublishingInfraVersion.Latest}\" BuildId=\"{buildId}\" InitialAssetsLocation=\"{initialAssetsLocation}\" IsReleaseOnlyPackageVersion=\"{isReleaseOnlyPackageVersion}\" IsStable=\"{isStable}\" />";

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                BuildEngine = buildEngine,
                ItemsToPush = new TaskItem[0],
                IsStableBuild = isStable,
                ManifestBuildId = buildId,
                IsReleaseOnlyPackageVersion = isReleaseOnlyPackageVersion,
                ManifestBuildData = new string[] { $"InitialAssetsLocation={initialAssetsLocation}" },
                AssetManifestPath = targetManifiestPath
            };

            task.Execute();

            Assert.AreEqual(expectedManifestContent, File.ReadAllText(targetManifiestPath));
        }

        [Test]
        public void UsesCustomPublishingVersion()
        {
            var targetManifiestPath = $"{Path.GetTempPath()}TestManifest-{Guid.NewGuid()}.xml";
            var buildId = "1.2.3";
            var initialAssetsLocation = "cloud";
            var isStable = false;
            var publishingInfraVersion = "456";
            var isReleaseOnlyPackageVersion = false;
            var expectedManifestContent = $"<Build PublishingVersion=\"{publishingInfraVersion}\" BuildId=\"{buildId}\" InitialAssetsLocation=\"{initialAssetsLocation}\" IsReleaseOnlyPackageVersion=\"{isReleaseOnlyPackageVersion}\" IsStable=\"{isStable}\" />";

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                BuildEngine = buildEngine,
                ItemsToPush = new TaskItem[0],
                IsStableBuild = isStable,
                IsReleaseOnlyPackageVersion = isReleaseOnlyPackageVersion,
                ManifestBuildId = buildId,
                ManifestBuildData = new string[] { $"InitialAssetsLocation={initialAssetsLocation}" },
                PublishingVersion = publishingInfraVersion,
                AssetManifestPath = targetManifiestPath
            };

            task.Execute();

            Assert.AreEqual(expectedManifestContent, File.ReadAllText(targetManifiestPath));
        }

        [Test]
        public void ProducesBasicManifest()
        {
            var buildEngine = new MockBuildEngine();
            ServiceProvider mockNupkgInfoProvider = new ServiceCollection()
                .AddLogging()
                .AddSingleton<NupkgInfo>(f => { return new MockNupkgInfo(); })
                .BuildServiceProvider();

            string expectedManifest = $@"<Build PublishingVersion=""2"" Name=""https://dnceng@dev.azure.com/dnceng/internal/test-repo"" BuildId=""12345.6"" Branch=""/refs/heads/branch"" Commit=""1234567890abcdef"" InitialAssetsLocation=""cloud"" IsReleaseOnlyPackageVersion=""False"" IsStable=""True"">
  <Package Id=""{Path.GetFileNameWithoutExtension(PackageA)}"" Version=""{MockNupkgInfo.MockNupkgVersion}"" Nonshipping=""true"" />
  <Package Id=""{Path.GetFileNameWithoutExtension(PackageB)}"" Version=""{MockNupkgInfo.MockNupkgVersion}"" Nonshipping=""false"" />
  <Blob Id=""{SampleManifest}"" Nonshipping=""false"" />
</Build>";

            PushToAzureDevOpsArtifacts task = ConstructPushToAzureDevOpsArtifactsTask(buildEngine, mockNupkgInfoProvider);

            task.Execute().Should().BeTrue();

            var manifest = task.FileSystem.FileReadAllText(TargetManifestPath);
            manifest.Should().Be(expectedManifest);
        }

        [Test]
        public void PublishFlatContainerManifest()
        {
            var buildEngine = new MockBuildEngine();
            ServiceProvider mockNupkgInfoProvider = new ServiceCollection()
                .AddLogging()
                .AddSingleton<NupkgInfo>(f => { return new MockNupkgInfo(); })
                .BuildServiceProvider();

            PushToAzureDevOpsArtifacts task = ConstructPushToAzureDevOpsArtifactsTask(buildEngine, mockNupkgInfoProvider);
            task.PublishFlatContainer = true;

            string expectedManifest = $@"<Build PublishingVersion=""2"" Name=""https://dnceng@dev.azure.com/dnceng/internal/test-repo"" BuildId=""12345.6"" Branch=""/refs/heads/branch"" Commit=""1234567890abcdef"" InitialAssetsLocation=""cloud"" IsReleaseOnlyPackageVersion=""False"" IsStable=""True"">
  <Blob Id=""{SampleManifest}"" Nonshipping=""false"" />
  <Blob Id=""{PackageA}"" Nonshipping=""true"" />
  <Blob Id=""{PackageB}"" Nonshipping=""false"" />
</Build>";

            task.Execute().Should().BeTrue();

            var manifest = task.FileSystem.FileReadAllText(TargetManifestPath);
            manifest.Should().Be(expectedManifest);
        }

        private static PushToAzureDevOpsArtifacts ConstructPushToAzureDevOpsArtifactsTask(IBuildEngine buildEngine, ServiceProvider nupkgInfoProvider)
        {
            return new PushToAzureDevOpsArtifacts
            {
                BuildEngine = buildEngine,
                NupkgInfoProvider = nupkgInfoProvider,
                FileSystem = MockFileSystem.CreateFromTaskItems(TaskItems),
                AssetManifestPath = TargetManifestPath,
                AzureDevOpsBuildId = 123456,
                AzureDevOpsCollectionUri = "https://dev.azure.com/dnceng/",
                AzureDevOpsProject = "internal",
                IsStableBuild = true,
                IsReleaseOnlyPackageVersion = false,
                ItemsToPush = TaskItems,
                ManifestBuildData = new string[] { $"InitialAssetsLocation=cloud" },
                ManifestBranch = "/refs/heads/branch",
                ManifestBuildId = "12345.6",
                ManifestCommit = "1234567890abcdef",
                ManifestRepoUri = "https://dnceng@dev.azure.com/dnceng/internal/test-repo",
            };
        }
    }

    internal class MockNupkgInfo : NupkgInfo
    {
        public static string MockNupkgVersion = "6.0.492";

        public override void Initialize(string path)
        {
            Id = Path.GetFileNameWithoutExtension(path);
            Version = MockNupkgVersion;
            Prerelease = "10f2c";
        }
    }

    internal class MockFileSystem : IFileSystem
    {
        public static MockFileSystem CreateFromTaskItems(TaskItem[] taskItems)
        {
            var mockFileSystem = new MockFileSystem();
            
            foreach (TaskItem taskItem in taskItems)
            {
                mockFileSystem.FileWriteAllText(taskItem.ItemSpec, "none");
            }

            return mockFileSystem;
        }

        private Dictionary<string, byte[]> _fakeFileSystem = new Dictionary<string, byte[]>();

        public DirectoryInfo DirectoryCreateDirectory(string path)
        {
            return null;
        }

        public bool FileExists(string path)
        {
            return _fakeFileSystem.ContainsKey(path);
        }

        public Stream FileOpenRead(string path)
        {
            return new MemoryStream(_fakeFileSystem[path]);
        }

        public string FileReadAllText(string path)
        {
            if (!_fakeFileSystem.TryGetValue(path, out byte[] content))
            {
                throw new FileNotFoundException();
            }

            return Encoding.UTF8.GetString(content);
        }

        public void FileWriteAllText(string path, string content)
        {
            _fakeFileSystem.Add(path, Encoding.UTF8.GetBytes(content));
        }
    }

}
