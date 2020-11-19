// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Feed.Tests.TestDoubles;
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
//using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    [TestFixture]
    public class PushToAzureDevOpsArtifactsTests
    {
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
        public void Test()
        {
            string targetManifestPath = Path.Combine(Path.GetTempPath(), $"TestManifest-{Guid.NewGuid()}.xml");
            string packageA = Path.Combine(Directory.GetCurrentDirectory(), "TestInputs/Nupkgs/test-package-a.zip");
            string packageB = Path.Combine(Directory.GetCurrentDirectory(), "TestInputs/Nupkgs/test-package-b.zip");
            string manifest2 = "TestInputs/Manifests/SampleV2.xml";

            TaskItem[] taskItems = new TaskItem[]
            {
                new TaskItem(packageA, new Dictionary<string, string>
                {
                    { "IsShipping", "false" },
                    { "ManifestArtifactData", "Nonshipping=true" },
                }),
                new TaskItem(packageB, new Dictionary<string, string>
                {
                    { "IsShipping", "true" },
                    { "ManifestArtifactData", "Nonshipping=false" },
                }),
                new TaskItem(manifest2, new Dictionary<string, string>
                {
                    { "IsShipping", "false" },
                    { "RelativeBlobPath", manifest2 },
                    { "ManifestArtifactData", "Nonshipping=false" },
                    { "PublishFlatContainer", "true" },
                }),
            };

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                FileSystem = MockFileSystem.CreateFromTaskItems(taskItems),
                BuildEngine = buildEngine,
                AssetManifestPath = targetManifestPath,
                AzureDevOpsBuildId = 123456,
                AzureDevOpsCollectionUri = "https://dev.azure.com/dnceng/",
                AzureDevOpsProject = "internal",
                IsStableBuild = true,
                IsReleaseOnlyPackageVersion = false,
                ItemsToPush = taskItems,
                ManifestBuildData = new string[] { $"InitialAssetsLocation=cloud" },
                ManifestBranch = "/refs/heads/branch",
                ManifestBuildId = "12345.6",
                ManifestCommit = "1234567890abcdef",
                ManifestRepoUri = "https://dnceng@dev.azure.com/dnceng/internal/test-repo",
            };

            Assert.That(task.Execute());

            var manifest = task.FileSystem.FileReadAllText(targetManifestPath);
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

        //public struct FakeFile
        //{
        //    public FileInfo FileInfo;
        //    public string FileContent;
        //}

        private Dictionary<string, string> _fakeFileSystem = new Dictionary<string, string>();

        public DirectoryInfo DirectoryCreateDirectory(string path)
        {
            DirectoryInfo realDirectoryInfo = new DirectoryInfo(path);
            Mock<DirectoryInfo> directoryInfo = new Mock<DirectoryInfo>(MockBehavior.Strict);

            directoryInfo.SetupGet(d => d.FullName).Returns(path);
            directoryInfo.SetupGet(d => d.Name).Returns(Path.GetDirectoryName(directoryInfo.Object.FullName));

            return directoryInfo.Object;
        }

        public bool FileExists(string path)
        {
            return _fakeFileSystem.ContainsKey(path);
        }

        public string FileReadAllText(string path)
        {
            if (!_fakeFileSystem.TryGetValue(path, out string content))
            {
                throw new FileNotFoundException();
            }

            return content;
        }

        public void FileWriteAllText(string path, string content)
        {
            //Mock<FileInfo> fileInfo = new Mock<FileInfo>(MockBehavior.Loose);
            //fileInfo.SetupGet(f => f.FullName).Returns(path);
            //fileInfo.SetupGet(f => f.Length).Returns(content.Length);
            //fileInfo.SetupGet(f => f.Name).Returns(Path.GetFileName(path));
            //fileInfo.SetupGet(f => f.CreationTime).Returns(DateTime.Now);
            //fileInfo.SetupGet(f => f.DirectoryName).Returns(Path.GetDirectoryName(path));
            //fileInfo.SetupGet(f => f.Extension).Returns(Path.GetExtension(path));

            _fakeFileSystem.Add(path, content);
        }
    }

}
