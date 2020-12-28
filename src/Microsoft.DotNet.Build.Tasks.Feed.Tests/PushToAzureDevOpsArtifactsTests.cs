// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Feed.Tests.TestDoubles;
using Microsoft.DotNet.Internal.DependencyInjection.Testing;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public class PushToAzureDevOpsArtifactsTests
    {
        private const string TARGET_MANIFEST_PATH = @"C:\manifests\TestManifest.xml";
        private const string PACKAGE_A = @"C:\packages\test-package-a.6.0.492.nupkg";
        private const string PACKAGE_B = @"C:\packages\test-package-b.6.0.492.nupkg";
        private const string SAMPLE_MANIFEST = @"C:\manifests\SampleManifest.xml";
        private const string NUPKG_VERSION = "6.0.492";

        private readonly TaskItem[] TaskItems = new TaskItem[]
        {
            new TaskItem(PACKAGE_A, new Dictionary<string, string>
            {
                { "RelativeBlobPath", PACKAGE_A },
                { "IsShipping", "false" },
                { "ManifestArtifactData", "Nonshipping=true" },
            }),
            new TaskItem(PACKAGE_B, new Dictionary<string, string>
            {
                { "RelativeBlobPath", PACKAGE_B },
                { "IsShipping", "true" },
                { "ManifestArtifactData", "Nonshipping=false" },
            }),
            new TaskItem(SAMPLE_MANIFEST, new Dictionary<string, string>
            {
                { "IsShipping", "false" },
                { "RelativeBlobPath", SAMPLE_MANIFEST },
                { "ManifestArtifactData", "Nonshipping=false" },
                { "PublishFlatContainer", "true" },
            }),
        };

        private PushToAzureDevOpsArtifacts ConstructPushToAzureDevOpsArtifactsTask(IBuildEngine buildEngine = null)
        {
            return new PushToAzureDevOpsArtifacts
            {
                BuildEngine = buildEngine ?? new MockBuildEngine(),
                AssetManifestPath = TARGET_MANIFEST_PATH,
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

        private void CreateMockServiceCollection(IServiceCollection collection)
        {
            Mock<IFileSystem> fileSystemMock = new();
            Mock<ISigningInformationModelFactory> signingInformationModelFactoryMock = new();
            Mock<IBlobArtifactModelFactory> blobArtifactModelFactoryMock = new();
            Mock<IPackageArtifactModelFactory> packageArtifactModelFactoryMock = new();
            Mock<IBuildModelFactory> buildModelFactoryMock = new();
            Mock<IPackageArchiveReaderFactory> packageArchiveReaderFactoryMock = new();
            Mock<INupkgInfoFactory> nupkgInfoFactoryMock = new();

            collection.TryAddSingleton(signingInformationModelFactoryMock.Object);
            collection.TryAddSingleton(blobArtifactModelFactoryMock.Object);
            collection.TryAddSingleton(packageArtifactModelFactoryMock.Object);
            collection.TryAddSingleton(buildModelFactoryMock.Object);
            collection.TryAddSingleton(fileSystemMock.Object);
            collection.TryAddSingleton(nupkgInfoFactoryMock.Object);
            collection.TryAddSingleton(packageArchiveReaderFactoryMock.Object);
        }

        [Fact]
        public void HasRecordedPublishingVersion()
        {
            var buildId = "1.2.3";
            var initialAssetsLocation = "cloud";
            var isStable = false;
            var isReleaseOnlyPackageVersion = false;
            var expectedManifestContent = $"<Build PublishingVersion=\"{(int)PublishingInfraVersion.Latest}\" BuildId=\"{buildId}\" InitialAssetsLocation=\"{initialAssetsLocation}\" IsReleaseOnlyPackageVersion=\"{isReleaseOnlyPackageVersion.ToString().ToLower()}\" IsStable=\"{isStable.ToString().ToLower()}\" />";

            var task = new PushToAzureDevOpsArtifacts
            {
                BuildEngine = new MockBuildEngine(),
                ItemsToPush = new TaskItem[0],
                IsStableBuild = isStable,
                ManifestBuildId = buildId,
                IsReleaseOnlyPackageVersion = isReleaseOnlyPackageVersion,
                ManifestBuildData = new string[] { $"InitialAssetsLocation={initialAssetsLocation}" },
                AssetManifestPath = TARGET_MANIFEST_PATH
            };

            // Mocks
            Mock<IFileSystem> fileSystemMock = new Mock<IFileSystem>();
            IList<string> actualPath = new List<string>();
            IList<string> actualBuildModel = new List<string>();
            fileSystemMock.Setup(m => m.WriteToFile(Capture.In(actualPath), Capture.In(actualBuildModel))).Verifiable();

            // Dependency Injection setup
            var collection = new ServiceCollection()
                .AddSingleton(fileSystemMock.Object)
                .AddSingleton<IBuildModelFactory, BuildModelFactory>();
            CreateMockServiceCollection(collection);
            task.ConfigureServices(collection);
            using var provider = collection.BuildServiceProvider();

            // Act and Assert
            task.InvokeExecute(provider).Should().BeTrue();
            actualPath[0].Should().Be(TARGET_MANIFEST_PATH);
            actualBuildModel[0].Should().Be(expectedManifestContent);
        }

        [Fact]
        public void UsesCustomPublishingVersion()
        {
            var buildId = "1.2.3";
            var initialAssetsLocation = "cloud";
            var isStable = false;
            var publishingInfraVersion = "456";
            var isReleaseOnlyPackageVersion = false;
            var expectedManifestContent = $"<Build PublishingVersion=\"{publishingInfraVersion}\" BuildId=\"{buildId}\" InitialAssetsLocation=\"{initialAssetsLocation}\" IsReleaseOnlyPackageVersion=\"{isReleaseOnlyPackageVersion.ToString().ToLower()}\" IsStable=\"{isStable.ToString().ToLower()}\" />";

            var task = new PushToAzureDevOpsArtifacts
            {
                BuildEngine = new MockBuildEngine(),
                ItemsToPush = new TaskItem[0],
                IsStableBuild = isStable,
                IsReleaseOnlyPackageVersion = isReleaseOnlyPackageVersion,
                ManifestBuildId = buildId,
                ManifestBuildData = new string[] { $"InitialAssetsLocation={initialAssetsLocation}" },
                PublishingVersion = publishingInfraVersion,
                AssetManifestPath = TARGET_MANIFEST_PATH
            };

            // Mocks
            Mock<IFileSystem> fileSystemMock = new Mock<IFileSystem>();
            IList<string> actualPath = new List<string>();
            IList<string> actualBuildModel = new List<string>();
            fileSystemMock.Setup(m => m.WriteToFile(Capture.In(actualPath), Capture.In(actualBuildModel))).Verifiable();

            // Dependency Injection setup
            var collection = new ServiceCollection()
                .AddSingleton(fileSystemMock.Object)
                .AddSingleton<IBuildModelFactory, BuildModelFactory>();
            CreateMockServiceCollection(collection);
            task.ConfigureServices(collection);
            using var provider = collection.BuildServiceProvider();

            // Act and Assert
            task.InvokeExecute(provider).Should().BeTrue();
            actualPath[0].Should().Be(TARGET_MANIFEST_PATH);
            actualBuildModel[0].Should().Be(expectedManifestContent);
        }

        [Fact]
        public void ProducesBasicManifest()
        {
            string expectedManifestContent = $@"<Build PublishingVersion=""2"" Name=""https://dnceng@dev.azure.com/dnceng/internal/test-repo"" BuildId=""12345.6"" Branch=""/refs/heads/branch"" Commit=""1234567890abcdef"" InitialAssetsLocation=""cloud"" IsReleaseOnlyPackageVersion=""false"" IsStable=""true"">
  <Package Id=""{Path.GetFileNameWithoutExtension(PACKAGE_A)}"" Version=""{NUPKG_VERSION}"" Nonshipping=""true"" />
  <Package Id=""{Path.GetFileNameWithoutExtension(PACKAGE_B)}"" Version=""{NUPKG_VERSION}"" Nonshipping=""false"" />
  <Blob Id=""{SAMPLE_MANIFEST}"" Nonshipping=""false"" />
</Build>";

            PushToAzureDevOpsArtifacts task = ConstructPushToAzureDevOpsArtifactsTask();

            // Mocks
            Mock<IFileSystem> fileSystemMock = new Mock<IFileSystem>();
            IList<string> actualPath = new List<string>();
            IList<string> actualBuildModel = new List<string>();
            IList<string> files = new List<string> { PACKAGE_A, PACKAGE_B, SAMPLE_MANIFEST };
            fileSystemMock.Setup(m => m.WriteToFile(Capture.In(actualPath), Capture.In(actualBuildModel))).Verifiable();
            fileSystemMock.Setup(m => m.FileExists(Capture.In(files))).Returns(true);

            Mock<INupkgInfoFactory> nupkgInfoFactoryMock = new Mock<INupkgInfoFactory>();
            IList<string> actualNupkgInfoPath = new List<string>();
            nupkgInfoFactoryMock.Setup(m => m.CreateNupkgInfo(PACKAGE_A)).Returns(new NupkgInfo(new PackageIdentity(
                id: Path.GetFileNameWithoutExtension(PACKAGE_A),
                version: new NuGetVersion(NUPKG_VERSION)
            )));
            nupkgInfoFactoryMock.Setup(m => m.CreateNupkgInfo(PACKAGE_B)).Returns(new NupkgInfo(new PackageIdentity(
                id: Path.GetFileNameWithoutExtension(PACKAGE_B),
                version: new NuGetVersion(NUPKG_VERSION)
            )));

            // Dependency Injection setup
            var collection = new ServiceCollection()
                .AddSingleton(fileSystemMock.Object)
                .AddSingleton<IBuildModelFactory, BuildModelFactory>()
                .AddSingleton<IPackageArtifactModelFactory, PackageArtifactModelFactory>()
                .AddSingleton<IBlobArtifactModelFactory, BlobArtifactModelFactory>()
                .AddSingleton(nupkgInfoFactoryMock.Object);
            CreateMockServiceCollection(collection);
            task.ConfigureServices(collection);
            using var provider = collection.BuildServiceProvider();

            // Act and Assert
            task.InvokeExecute(provider).Should().BeTrue();
            actualPath[0].Should().Be(TARGET_MANIFEST_PATH);
            actualBuildModel[0].Should().Be(expectedManifestContent);
        }

        [Fact]
        public void PublishFlatContainerManifest()
        {
            PushToAzureDevOpsArtifacts task = ConstructPushToAzureDevOpsArtifactsTask();
            task.PublishFlatContainer = true;

            XDocument expectedBuildModel = new XDocument(
                new XElement("Build", 
                    new XAttribute("PublishingVersion", (int)PublishingInfraVersion.Latest),
                    new XAttribute("Name", "https://dnceng@dev.azure.com/dnceng/internal/test-repo"),
                    new XAttribute("BuildId", "12345.6"), 
                    new XAttribute("Branch", "/refs/heads/branch"),
                    new XAttribute("Commit", "1234567890abcdef"),
                    new XAttribute("InitialAssetsLocation", "cloud"),
                    new XAttribute("IsReleaseOnlyPackageVersion", "false"),
                    new XAttribute("IsStable", "true"),
                        new XElement("Blob", 
                            new XAttribute("Id", SAMPLE_MANIFEST),
                            new XAttribute("Nonshipping", "false")
                        ),
                        new XElement("Blob",
                            new XAttribute("Id", PACKAGE_A),
                            new XAttribute("Nonshipping", "true")
                        ),
                        new XElement("Blob",
                            new XAttribute("Id", PACKAGE_B),
                            new XAttribute("Nonshipping", "false")
                        )
                )
            );

            // Mocks
            Mock<IFileSystem> fileSystemMock = new Mock<IFileSystem>();
            IList<string> actualPath = new List<string>();
            IList<string> actualBuildModel = new List<string>();
            IList<string> files = new List<string> { PACKAGE_A, PACKAGE_B, SAMPLE_MANIFEST };
            fileSystemMock.Setup(m => m.WriteToFile(Capture.In(actualPath), Capture.In(actualBuildModel))).Verifiable();
            fileSystemMock.Setup(m => m.FileExists(Capture.In(files))).Returns(true);

            Mock<INupkgInfoFactory> nupkgInfoFactoryMock = new Mock<INupkgInfoFactory>();
            IList<string> actualNupkgInfoPath = new List<string>();
            nupkgInfoFactoryMock.Setup(m => m.CreateNupkgInfo(PACKAGE_A)).Returns(new NupkgInfo(new PackageIdentity(
                id: Path.GetFileNameWithoutExtension(PACKAGE_A),
                version: new NuGetVersion(NUPKG_VERSION)
            )));
            nupkgInfoFactoryMock.Setup(m => m.CreateNupkgInfo(PACKAGE_B)).Returns(new NupkgInfo(new PackageIdentity(
                id: Path.GetFileNameWithoutExtension(PACKAGE_B),
                version: new NuGetVersion(NUPKG_VERSION)
            )));

            // Dependency Injection setup
            var collection = new ServiceCollection()
                .AddSingleton(fileSystemMock.Object)
                .AddSingleton<IBuildModelFactory, BuildModelFactory>()
                .AddSingleton<IPackageArtifactModelFactory, PackageArtifactModelFactory>()
                .AddSingleton<IBlobArtifactModelFactory, BlobArtifactModelFactory>()
                .AddSingleton(nupkgInfoFactoryMock.Object);
            CreateMockServiceCollection(collection);
            task.ConfigureServices(collection);
            using var provider = collection.BuildServiceProvider();

            // Act and Assert
            task.InvokeExecute(provider).Should().BeTrue();
            actualPath[0].Should().Be(TARGET_MANIFEST_PATH);
            actualBuildModel[0].Should().BeEquivalentTo(expectedBuildModel.ToString());
        }

        [Fact]
        public void IsNotStableBuildPath()
        {
            var buildEngine = new MockBuildEngine();
            PushToAzureDevOpsArtifacts task = ConstructPushToAzureDevOpsArtifactsTask(buildEngine);
            task.IsStableBuild = false;

            string expectedManifestContent = $@"<Build PublishingVersion=""{(int)PublishingInfraVersion.Latest}"" Name=""https://dnceng@dev.azure.com/dnceng/internal/test-repo"" BuildId=""12345.6"" Branch=""/refs/heads/branch"" Commit=""1234567890abcdef"" InitialAssetsLocation=""cloud"" IsReleaseOnlyPackageVersion=""false"" IsStable=""false"">
  <Package Id=""{Path.GetFileNameWithoutExtension(PACKAGE_A)}"" Version=""{NUPKG_VERSION}"" Nonshipping=""true"" />
  <Package Id=""{Path.GetFileNameWithoutExtension(PACKAGE_B)}"" Version=""{NUPKG_VERSION}"" Nonshipping=""false"" />
  <Blob Id=""{SAMPLE_MANIFEST}"" Nonshipping=""false"" />
</Build>";

            // Mocks
            Mock<IFileSystem> fileSystemMock = new Mock<IFileSystem>();
            IList<string> actualPath = new List<string>();
            IList<string> actualBuildModel = new List<string>();
            IList<string> files = new List<string> { PACKAGE_A, PACKAGE_B, SAMPLE_MANIFEST };
            fileSystemMock.Setup(m => m.WriteToFile(Capture.In(actualPath), Capture.In(actualBuildModel))).Verifiable();
            fileSystemMock.Setup(m => m.FileExists(Capture.In(files))).Returns(true);

            Mock<INupkgInfoFactory> nupkgInfoFactoryMock = new Mock<INupkgInfoFactory>();
            IList<string> actualNupkgInfoPath = new List<string>();
            nupkgInfoFactoryMock.Setup(m => m.CreateNupkgInfo(PACKAGE_A)).Returns(new NupkgInfo(new PackageIdentity(
                id: Path.GetFileNameWithoutExtension(PACKAGE_A),
                version: new NuGetVersion(NUPKG_VERSION)
            )));
            nupkgInfoFactoryMock.Setup(m => m.CreateNupkgInfo(PACKAGE_B)).Returns(new NupkgInfo(new PackageIdentity(
                id: Path.GetFileNameWithoutExtension(PACKAGE_B),
                version: new NuGetVersion(NUPKG_VERSION)
            )));

            // Dependency Injection setup
            var collection = new ServiceCollection()
                .AddSingleton(fileSystemMock.Object)
                .AddSingleton<IBuildModelFactory, BuildModelFactory>()
                .AddSingleton<IPackageArtifactModelFactory, PackageArtifactModelFactory>()
                .AddSingleton<IBlobArtifactModelFactory, BlobArtifactModelFactory>()
                .AddSingleton(nupkgInfoFactoryMock.Object);
            CreateMockServiceCollection(collection);
            task.ConfigureServices(collection);
            using var provider = collection.BuildServiceProvider();

            // Act and Assert
            //task.BuildEngine.
            var result = task.InvokeExecute(provider);
            buildEngine.BuildErrorEvents.Count.Should().Be(0);
            buildEngine.BuildErrorEvents.ForEach(x => Console.WriteLine(x.Message));
            result.Should().BeTrue();
            actualPath[0].Should().Be(TARGET_MANIFEST_PATH);
            actualBuildModel[0].Should().Be(expectedManifestContent);
        }

        [Fact]
        public void IsReleaseOnlyPackageVersionPath()
        {
            PushToAzureDevOpsArtifacts task = ConstructPushToAzureDevOpsArtifactsTask();
            task.IsReleaseOnlyPackageVersion = true;

            string expectedManifestContent = $@"<Build PublishingVersion=""{(int)PublishingInfraVersion.Latest}"" Name=""https://dnceng@dev.azure.com/dnceng/internal/test-repo"" BuildId=""12345.6"" Branch=""/refs/heads/branch"" Commit=""1234567890abcdef"" InitialAssetsLocation=""cloud"" IsReleaseOnlyPackageVersion=""true"" IsStable=""true"">
  <Package Id=""{Path.GetFileNameWithoutExtension(PACKAGE_A)}"" Version=""{NUPKG_VERSION}"" Nonshipping=""true"" />
  <Package Id=""{Path.GetFileNameWithoutExtension(PACKAGE_B)}"" Version=""{NUPKG_VERSION}"" Nonshipping=""false"" />
  <Blob Id=""{SAMPLE_MANIFEST}"" Nonshipping=""false"" />
</Build>";

            // Mocks
            Mock<IFileSystem> fileSystemMock = new Mock<IFileSystem>();
            IList<string> actualPath = new List<string>();
            IList<string> actualBuildModel = new List<string>();
            IList<string> files = new List<string> { PACKAGE_A, PACKAGE_B, SAMPLE_MANIFEST };
            fileSystemMock.Setup(m => m.WriteToFile(Capture.In(actualPath), Capture.In(actualBuildModel))).Verifiable();
            fileSystemMock.Setup(m => m.FileExists(Capture.In(files))).Returns(true);

            Mock<INupkgInfoFactory> nupkgInfoFactoryMock = new Mock<INupkgInfoFactory>();
            IList<string> actualNupkgInfoPath = new List<string>();
            nupkgInfoFactoryMock.Setup(m => m.CreateNupkgInfo(PACKAGE_A)).Returns(new NupkgInfo(new PackageIdentity(
                id: Path.GetFileNameWithoutExtension(PACKAGE_A),
                version: new NuGetVersion(NUPKG_VERSION)
            )));
            nupkgInfoFactoryMock.Setup(m => m.CreateNupkgInfo(PACKAGE_B)).Returns(new NupkgInfo(new PackageIdentity(
                id: Path.GetFileNameWithoutExtension(PACKAGE_B),
                version: new NuGetVersion(NUPKG_VERSION)
            )));

            // Dependency Injection setup
            var collection = new ServiceCollection()
                .AddSingleton(fileSystemMock.Object)
                .AddSingleton<IBuildModelFactory, BuildModelFactory>()
                .AddSingleton<IPackageArtifactModelFactory, PackageArtifactModelFactory>()
                .AddSingleton<IBlobArtifactModelFactory, BlobArtifactModelFactory>()
                .AddSingleton(nupkgInfoFactoryMock.Object);
            CreateMockServiceCollection(collection);
            task.ConfigureServices(collection);
            using var provider = collection.BuildServiceProvider();

            // Act and Assert
            task.InvokeExecute(provider).Should().BeTrue();
            actualPath[0].Should().Be(TARGET_MANIFEST_PATH);
            actualBuildModel[0].Should().Be(expectedManifestContent);
        }


        [Fact]
        public void SigningInfoInManifest()
        {
            PushToAzureDevOpsArtifacts task = ConstructPushToAzureDevOpsArtifactsTask();
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
                    { "PublicKeyToken", "123456789ABCDEF0" },
                    { "CertificateName", "BestCert" }
                }),
                new TaskItem("VeryTrashStrongName", new Dictionary<string, string>
                {
                    { "PublicKeyToken", "00FEDCBA98765432" },
                    { "CertificateName", "WorstCert" }
                }),
            };
            task.ItemsToSign = new ITaskItem[]
            {
                new TaskItem(PACKAGE_A),
                new TaskItem(PACKAGE_B),
            };

            string expectedManifestContent = $@"<Build PublishingVersion=""{(int)PublishingInfraVersion.Latest}"" Name=""https://dnceng@dev.azure.com/dnceng/internal/test-repo"" BuildId=""12345.6"" Branch=""/refs/heads/branch"" Commit=""1234567890abcdef"" InitialAssetsLocation=""cloud"" IsReleaseOnlyPackageVersion=""false"" IsStable=""true"">
  <Package Id=""test-package-a"" Version=""{NUPKG_VERSION}"" Nonshipping=""true"" />
  <Package Id=""test-package-b"" Version=""{NUPKG_VERSION}"" Nonshipping=""false"" />
  <Blob Id=""{SAMPLE_MANIFEST}"" Nonshipping=""false"" />
  <SigningInformation>
    <FileExtensionSignInfo Include="".dll"" CertificateName=""TestSigningCert"" />
    <FileExtensionSignInfo Include="".nupkg"" CertificateName=""TestNupkg"" />
    <FileExtensionSignInfo Include="".zip"" CertificateName=""None"" />
    <FileSignInfo Include=""Best.dll"" CertificateName=""BestCert"" />
    <FileSignInfo Include=""Worst.dll"" CertificateName=""WorstCert"" />
    <CertificatesSignInfo Include=""BestCert"" DualSigningAllowed=""true"" />
    <CertificatesSignInfo Include=""WorstCert"" DualSigningAllowed=""false"" />
    <ItemsToSign Include=""test-package-a.6.0.492.nupkg"" />
    <ItemsToSign Include=""test-package-b.6.0.492.nupkg"" />
    <StrongNameSignInfo Include=""VeryCoolStrongName"" PublicKeyToken=""123456789ABCDEF0"" CertificateName=""BestCert"" />
    <StrongNameSignInfo Include=""VeryTrashStrongName"" PublicKeyToken=""00FEDCBA98765432"" CertificateName=""WorstCert"" />
  </SigningInformation>
</Build>";

            // Mocks
            Mock<IFileSystem> fileSystemMock = new Mock<IFileSystem>();
            IList<string> actualPath = new List<string>();
            IList<string> actualBuildModel = new List<string>();
            IList<string> files = new List<string> { PACKAGE_A, PACKAGE_B, SAMPLE_MANIFEST };
            fileSystemMock.Setup(m => m.WriteToFile(Capture.In(actualPath), Capture.In(actualBuildModel))).Verifiable();
            fileSystemMock.Setup(m => m.FileExists(Capture.In(files))).Returns(true);

            Mock<INupkgInfoFactory> nupkgInfoFactoryMock = new Mock<INupkgInfoFactory>();
            IList<string> actualNupkgInfoPath = new List<string>();
            nupkgInfoFactoryMock.Setup(m => m.CreateNupkgInfo(PACKAGE_A)).Returns(new NupkgInfo(new PackageIdentity(
                id: "test-package-a",
                version: new NuGetVersion(NUPKG_VERSION)
            )));
            nupkgInfoFactoryMock.Setup(m => m.CreateNupkgInfo(PACKAGE_B)).Returns(new NupkgInfo(new PackageIdentity(
                id: "test-package-b",
                version: new NuGetVersion(NUPKG_VERSION)
            )));

            // Dependency Injection setup
            var collection = new ServiceCollection()
                .AddSingleton(fileSystemMock.Object)
                .AddSingleton<IBuildModelFactory, BuildModelFactory>()
                .AddSingleton<IPackageArtifactModelFactory, PackageArtifactModelFactory>()
                .AddSingleton<IBlobArtifactModelFactory, BlobArtifactModelFactory>()
                .AddSingleton<ISigningInformationModelFactory, SigningInformationModelFactory>()
                .AddSingleton(nupkgInfoFactoryMock.Object);
            CreateMockServiceCollection(collection);
            task.ConfigureServices(collection);
            using var provider = collection.BuildServiceProvider();

            // Act and Assert
            task.InvokeExecute(provider).Should().BeTrue();
            actualPath[0].Should().Be(TARGET_MANIFEST_PATH);
            actualBuildModel[0].Should().Be(expectedManifestContent);
        }

        [Fact]
        public void AreDependenciesRegistered()
        {
            PushToAzureDevOpsArtifacts task = new PushToAzureDevOpsArtifacts();

            var collection = new ServiceCollection();
            task.ConfigureServices(collection);
            var provider = collection.BuildServiceProvider();

            foreach(var dependency in task.GetExecuteParameterTypes())
            {
                var service = provider.GetRequiredService(dependency);
                service.Should().NotBeNull();
            }

            DependencyInjectionValidation.IsDependencyResolutionCoherent(
                    s =>
                    {
                        task.ConfigureServices(s);
                    },
                    out string message
                )
                .Should()
                .BeTrue(message);
        }
    }
}
