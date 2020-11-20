// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Feed.Tests.TestDoubles;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
            var targetManifestPath = $"{Path.GetTempPath()}TestManifest-{Guid.NewGuid()}.xml";
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
                AssetManifestPath = targetManifestPath
            };

            task.Execute().Should().BeTrue();

            File.ReadAllText(targetManifestPath).Should().Be(expectedManifestContent);
        }

        [Test]
        public void UsesCustomPublishingVersion()
        {
            var targetManifestPath = $"{Path.GetTempPath()}TestManifest-{Guid.NewGuid()}.xml";
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
                AssetManifestPath = targetManifestPath
            };

            task.Execute().Should().BeTrue();

            File.ReadAllText(targetManifestPath).Should().Be(expectedManifestContent);
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

            string expectedManifest = $@"<Build PublishingVersion=""{(int)PublishingInfraVersion.Latest}"" Name=""https://dnceng@dev.azure.com/dnceng/internal/test-repo"" BuildId=""12345.6"" Branch=""/refs/heads/branch"" Commit=""1234567890abcdef"" InitialAssetsLocation=""cloud"" IsReleaseOnlyPackageVersion=""False"" IsStable=""True"">
  <Blob Id=""{SampleManifest}"" Nonshipping=""false"" />
  <Blob Id=""{PackageA}"" Nonshipping=""true"" />
  <Blob Id=""{PackageB}"" Nonshipping=""false"" />
</Build>";

            task.Execute().Should().BeTrue();

            var manifest = task.FileSystem.FileReadAllText(TargetManifestPath);
            manifest.Should().Be(expectedManifest);
        }

        [Test]
        public void IsNotStableBuildPath()
        {
            var buildEngine = new MockBuildEngine();
            ServiceProvider mockNupkgInfoProvider = new ServiceCollection()
                .AddLogging()
                .AddSingleton<NupkgInfo>(f => { return new MockNupkgInfo(); })
                .BuildServiceProvider();

            PushToAzureDevOpsArtifacts task = ConstructPushToAzureDevOpsArtifactsTask(buildEngine, mockNupkgInfoProvider);
            task.IsStableBuild = false;

            string expectedManifest = $@"<Build PublishingVersion=""{(int)PublishingInfraVersion.Latest}"" Name=""https://dnceng@dev.azure.com/dnceng/internal/test-repo"" BuildId=""12345.6"" Branch=""/refs/heads/branch"" Commit=""1234567890abcdef"" InitialAssetsLocation=""cloud"" IsReleaseOnlyPackageVersion=""False"" IsStable=""False"">
  <Package Id=""{Path.GetFileNameWithoutExtension(PackageA)}"" Version=""{MockNupkgInfo.MockNupkgVersion}"" Nonshipping=""true"" />
  <Package Id=""{Path.GetFileNameWithoutExtension(PackageB)}"" Version=""{MockNupkgInfo.MockNupkgVersion}"" Nonshipping=""false"" />
  <Blob Id=""{SampleManifest}"" Nonshipping=""false"" />
</Build>";

            task.Execute().Should().BeTrue();

            var manifest = task.FileSystem.FileReadAllText(task.AssetManifestPath);
            manifest.Should().Be(expectedManifest);
        }

        [Test]
        public void IsReleaseOnlyPackageVersionPath()
        {
            var buildEngine = new MockBuildEngine();
            ServiceProvider mockNupkgInfoProvider = new ServiceCollection()
                .AddLogging()
                .AddSingleton<NupkgInfo>(f => { return new MockNupkgInfo(); })
                .BuildServiceProvider();

            PushToAzureDevOpsArtifacts task = ConstructPushToAzureDevOpsArtifactsTask(buildEngine, mockNupkgInfoProvider);
            task.IsReleaseOnlyPackageVersion = true;

            string expectedManifest = $@"<Build PublishingVersion=""{(int)PublishingInfraVersion.Latest}"" Name=""https://dnceng@dev.azure.com/dnceng/internal/test-repo"" BuildId=""12345.6"" Branch=""/refs/heads/branch"" Commit=""1234567890abcdef"" InitialAssetsLocation=""cloud"" IsReleaseOnlyPackageVersion=""True"" IsStable=""True"">
  <Package Id=""{Path.GetFileNameWithoutExtension(PackageA)}"" Version=""{MockNupkgInfo.MockNupkgVersion}"" Nonshipping=""true"" />
  <Package Id=""{Path.GetFileNameWithoutExtension(PackageB)}"" Version=""{MockNupkgInfo.MockNupkgVersion}"" Nonshipping=""false"" />
  <Blob Id=""{SampleManifest}"" Nonshipping=""false"" />
</Build>";

            task.Execute().Should().BeTrue();

            var manifest = task.FileSystem.FileReadAllText(task.AssetManifestPath);
            manifest.Should().Be(expectedManifest);
        }

        [Test]
        public void SigningInfoInManifest()
        {
            var buildEngine = new MockBuildEngine();
            ServiceProvider mockNupkgInfoProvider = new ServiceCollection()
                .AddLogging()
                .AddSingleton<NupkgInfo>(f => { return new MockNupkgInfo(); })
                .BuildServiceProvider();

            PushToAzureDevOpsArtifacts task = ConstructPushToAzureDevOpsArtifactsTask(buildEngine, mockNupkgInfoProvider);
            task.FileExtensionSignInfo = new ITaskItem[]
            {
                new TaskItem(".dll", new Dictionary<string, string>
                {
                    { "CertificateName", "TestSigningCert" }
                }),
                new TaskItem(".nupkg", new Dictionary<string, string>
                {
                    { "CertificateName", "TestNupkg" }
                }),
                new TaskItem(".zip", new Dictionary<string, string>
                {
                    { "CertificateName", "None" }
                }),
            };
            task.FileSignInfo = new ITaskItem[]
            {
                new TaskItem("Best.dll", new Dictionary<string, string>
                {
                    { "CertificateName", "BestCert" }
                }),
                new TaskItem("Worst.dll", new Dictionary<string, string>
                {
                    { "CertificateName", "WorstCert" }
                }),
            };
            task.CertificatesSignInfo = new ITaskItem[]
            {
                new TaskItem("BestCert", new Dictionary<string, string>
                {
                    { "DualSigningAllowed", "true" }
                }),
                new TaskItem("WorstCert", new Dictionary<string, string>
                {
                    { "DualSigningAllowed", "false" }
                }),
            };
            task.StrongNameSignInfo = new ITaskItem[]
            {
                new TaskItem("VeryCoolStrongName", new Dictionary<string, string>
                {
                    { "PublicKeyToken", "123456789ABCDEF00" },
                    { "CertificateName", "BestCert" }
                }),
                new TaskItem("VeryTrashStrongName", new Dictionary<string, string>
                {
                    { "PublicKeyToken", "00FEDCBA987654321" },
                    { "CertificateName", "WorstCert" }
                }),
            };
            task.ItemsToSign = new ITaskItem[]
            {
                new TaskItem(PackageA),
                new TaskItem(PackageB),
            };

            string expectedManifest = $@"<Build PublishingVersion=""{(int)PublishingInfraVersion.Latest}"" Name=""https://dnceng@dev.azure.com/dnceng/internal/test-repo"" BuildId=""12345.6"" Branch=""/refs/heads/branch"" Commit=""1234567890abcdef"" InitialAssetsLocation=""cloud"" IsReleaseOnlyPackageVersion=""False"" IsStable=""True"">
  <Package Id=""{Path.GetFileNameWithoutExtension(PackageA)}"" Version=""{MockNupkgInfo.MockNupkgVersion}"" Nonshipping=""true"" />
  <Package Id=""{Path.GetFileNameWithoutExtension(PackageB)}"" Version=""{MockNupkgInfo.MockNupkgVersion}"" Nonshipping=""false"" />
  <Blob Id=""{SampleManifest}"" Nonshipping=""false"" />
  <SigningInformation>
    <FileExtensionSignInfo Include="".dll"" CertificateName=""TestSigningCert"" />
    <FileExtensionSignInfo Include="".nupkg"" CertificateName=""TestNupkg"" />
    <FileExtensionSignInfo Include="".zip"" CertificateName=""None"" />
    <FileSignInfo Include=""Best.dll"" CertificateName=""BestCert"" />
    <FileSignInfo Include=""Worst.dll"" CertificateName=""WorstCert"" />
    <CertificatesSignInfo Include=""BestCert"" DualSigningAllowed=""true"" />
    <CertificatesSignInfo Include=""WorstCert"" DualSigningAllowed=""false"" />
    <ItemsToSign Include=""test-package-a.nupkg"" />
    <ItemsToSign Include=""test-package-b.nupkg"" />
    <StrongNameSignInfo Include=""VeryCoolStrongName"" PublicKeyToken=""123456789ABCDEF00"" CertificateName=""BestCert"" />
    <StrongNameSignInfo Include=""VeryTrashStrongName"" PublicKeyToken=""00FEDCBA987654321"" CertificateName=""WorstCert"" />
  </SigningInformation>
</Build>";

            task.Execute().Should().BeTrue();

            var manifest = task.FileSystem.FileReadAllText(task.AssetManifestPath);
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
            new FileInfo(path); // confirm path is a valid file path
            _fakeFileSystem.Add(path, Encoding.UTF8.GetBytes(content));
        }
    }

}
