// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests
{
    public class PushToAzureDevOpsArtifactsTests : IDisposable
    {
        private const string Configuration =
#if DEBUG
            "Debug"
#else
            "Release"
#endif
            ;
        private const string DefaultManifestBuildId = "no build id provided";
        private const string SolutionName = "Arcade.sln";
        private const string PackageId = "Microsoft.DotNet.VersionTools";

        private static readonly string BlobPath = new Uri(typeof(PushToAzureDevOpsArtifactsTests).Assembly.CodeBase).LocalPath;
        private static readonly string BlobName = Path.GetFileName(BlobPath);
        private static readonly string ThisDirectory = Path.GetDirectoryName(BlobPath);

        private static readonly string ManifestLocation = Guid.NewGuid().ToString("N");
        private static readonly string[] ManifestBuildData = new[] { $"Location={ManifestLocation}" };

        private static readonly string ManifestBranch = Guid.NewGuid().ToString("N");
        private static readonly string ManifestBuildId = Guid.NewGuid().ToString("N");
        private static readonly string ManifestCommit = Guid.NewGuid().ToString("N");
        private static readonly string ManifestRepoUri = Guid.NewGuid().ToString("N");
        private static readonly string PackageName;
        private static readonly string PackagePath;
        private static readonly string PackageVersion;
        private static readonly string SymbolsName;
        private static readonly string SymbolsPath;

        private readonly string assetsTemporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        private readonly string manifestsDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        static PushToAzureDevOpsArtifactsTests()
        {
            var solutionDirectory = ThisDirectory;
            while (!File.Exists(Path.Combine(solutionDirectory, SolutionName)))
            {
                solutionDirectory = Path.Combine(solutionDirectory, "..");
            }

            solutionDirectory = Path.GetFullPath(solutionDirectory);
            var packagesDirectory = Path.Combine(solutionDirectory, "artifacts", "packages", Configuration, "NonShipping");
            var files = Directory.GetFiles(packagesDirectory, $"{PackageId}.*.nupkg");

            PackagePath = files[files.Length - 2];
            PackageName = Path.GetFileName(PackagePath);
            SymbolsPath = files[files.Length - 1];
            SymbolsName = Path.GetFileName(SymbolsPath);

            var filename = Path.GetFileNameWithoutExtension(PackagePath);
            PackageVersion = filename.Replace($"{PackageId}.", "");
        }

        [Fact]
        public void Execute_CreatesManifestForBlob()
        {
            var relativeBlobPath = $"my/assets/{BlobName}";
            var expectedManifest =
$@"<Build Name=""{ManifestRepoUri}"" BuildId=""{DefaultManifestBuildId}"" Branch=""{ManifestBranch}"" Commit=""{ManifestCommit}"" IsStable=""False"" Location=""{ManifestLocation}"">
  <Blob Id=""{relativeBlobPath}"" />
</Build>";
            var expectedLocation = $"{assetsTemporaryDirectory}/{BlobName}";

            var assetManifestPath = Path.Combine(manifestsDirectory, $"{nameof(Execute_CreatesManifestForBlob)}.xml");
            var itemsToPush = new[]
            {
                new TaskItem(BlobPath),
            };
            itemsToPush[0].SetMetadata("PublishFlatContainer", "true");
            itemsToPush[0].SetMetadata("RelativeBlobPath", relativeBlobPath);

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                AssetManifestPath = assetManifestPath,
                AssetsTemporaryDirectory = assetsTemporaryDirectory,
                BuildEngine = buildEngine,
                ItemsToPush = itemsToPush,
                ManifestBranch = ManifestBranch,
                ManifestBuildData = ManifestBuildData,
                ManifestCommit = ManifestCommit,
                ManifestRepoUri = ManifestRepoUri,
            };

            var result = task.Execute();

            Assert.True(result);
            var assets = File.ReadAllText(assetManifestPath);
            Assert.Equal(expectedManifest, assets, ignoreLineEndingDifferences: true);

            var events = buildEngine.BuildMessageEvents;

            // Event about creating AssetsTemporaryDirectory may or may not be written.
            Assert.InRange(events.Count, 3, 4);

            // Second-to-last message event is vso push to artifacts.
            var messageEvent = events[events.Count - 2];
            Assert.Contains("BlobArtifacts", messageEvent.Message);
            Assert.Contains(expectedLocation, messageEvent.Message);
        }

        [Fact]
        public void Execute_CreatesManifestForBlob_WhenPublishFlatContainerTrue()
        {
            var relativeBlobPath = $"my/assets/{BlobName}";
            var expectedManifest =
$@"<Build Name=""{ManifestRepoUri}"" BuildId=""{DefaultManifestBuildId}"" Branch=""{ManifestBranch}"" Commit=""{ManifestCommit}"" IsStable=""False"" Location=""{ManifestLocation}"">
  <Blob Id=""{relativeBlobPath}"" />
</Build>";
            var expectedLocation = $"{assetsTemporaryDirectory}/{BlobName}";

            var assetManifestPath = Path.Combine(
                manifestsDirectory,
                $"{nameof(Execute_CreatesManifestForBlob_WhenPublishFlatContainerTrue)}.xml");
            var itemsToPush = new[]
            {
                new TaskItem(BlobPath),
            };
            itemsToPush[0].SetMetadata("RelativeBlobPath", relativeBlobPath);

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                AssetManifestPath = assetManifestPath,
                AssetsTemporaryDirectory = assetsTemporaryDirectory,
                BuildEngine = buildEngine,
                ItemsToPush = itemsToPush,
                ManifestBranch = ManifestBranch,
                ManifestBuildData = ManifestBuildData,
                ManifestCommit = ManifestCommit,
                ManifestRepoUri = ManifestRepoUri,
                PublishFlatContainer = true,
            };

            var result = task.Execute();

            Assert.True(result);
            var assets = File.ReadAllText(assetManifestPath);
            Assert.Equal(expectedManifest, assets, ignoreLineEndingDifferences: true);

            var events = buildEngine.BuildMessageEvents;
            Assert.InRange(events.Count, 3, 4);

            var messageEvent = events[events.Count - 2];
            Assert.Contains("BlobArtifacts", messageEvent.Message);
            Assert.Contains(expectedLocation, messageEvent.Message);
        }

        // Creates a PackageArtifactModel.
        [Fact]
        public void Execute_CreatesManifestForPackage()
        {
            var expectedManifest =
$@"<Build Name=""{ManifestRepoUri}"" BuildId=""{DefaultManifestBuildId}"" Branch=""{ManifestBranch}"" Commit=""{ManifestCommit}"" IsStable=""False"" Location=""{ManifestLocation}"">
  <Package Id=""{PackageId}"" Version=""{PackageVersion}"" />
</Build>";
            var expectedLocation = $"{assetsTemporaryDirectory}/{PackageName}";

            var assetManifestPath = Path.Combine(
                manifestsDirectory,
                $"{nameof(Execute_CreatesManifestForPackage)}.xml");
            var itemsToPush = new[]
            {
                new TaskItem(PackagePath),
            };

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                AssetManifestPath = assetManifestPath,
                AssetsTemporaryDirectory = assetsTemporaryDirectory,
                BuildEngine = buildEngine,
                ItemsToPush = itemsToPush,
                ManifestBranch = ManifestBranch,
                ManifestBuildData = ManifestBuildData,
                ManifestCommit = ManifestCommit,
                ManifestRepoUri = ManifestRepoUri,
            };

            var result = task.Execute();

            Assert.True(result);
            var assets = File.ReadAllText(assetManifestPath);
            Assert.Equal(expectedManifest, assets, ignoreLineEndingDifferences: true);

            var events = buildEngine.BuildMessageEvents;
            Assert.InRange(events.Count, 3, 4);

            var messageEvent = events[events.Count - 2];
            Assert.Contains("PackageArtifacts", messageEvent.Message);
            Assert.Contains(expectedLocation, messageEvent.Message);
        }

        // Creates a BlobArtifactModel.
        [Fact]
        public void Execute_CreatesManifestForPackage_WhenPublishFlatContainerTrue()
        {
            var relativePackagePath = $"my/assets/{PackageName}";
            var expectedManifest =
$@"<Build Name=""{ManifestRepoUri}"" BuildId=""{DefaultManifestBuildId}"" Branch=""{ManifestBranch}"" Commit=""{ManifestCommit}"" IsStable=""False"" Location=""{ManifestLocation}"">
  <Blob Id=""{relativePackagePath}"" />
</Build>";
            var expectedLocation = $"{assetsTemporaryDirectory}/{PackageName}";

            var assetManifestPath = Path.Combine(
                manifestsDirectory,
                $"{nameof(Execute_CreatesManifestForPackage_WhenPublishFlatContainerTrue)}.xml");
            var itemsToPush = new[]
            {
                new TaskItem(PackagePath),
            };
            itemsToPush[0].SetMetadata("RelativeBlobPath", relativePackagePath);

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                AssetManifestPath = assetManifestPath,
                AssetsTemporaryDirectory = assetsTemporaryDirectory,
                BuildEngine = buildEngine,
                ItemsToPush = itemsToPush,
                ManifestBranch = ManifestBranch,
                ManifestBuildData = ManifestBuildData,
                ManifestCommit = ManifestCommit,
                ManifestRepoUri = ManifestRepoUri,
                PublishFlatContainer = true,
            };

            var result = task.Execute();

            Assert.True(result);
            var assets = File.ReadAllText(assetManifestPath);
            Assert.Equal(expectedManifest, assets, ignoreLineEndingDifferences: true);

            var events = buildEngine.BuildMessageEvents;
            Assert.InRange(events.Count, 3, 4);

            var messageEvent = events[events.Count - 2];
            Assert.Contains("BlobArtifacts", messageEvent.Message);
            Assert.Contains(expectedLocation, messageEvent.Message);
        }

        // Sets %(RelativeBlobPath) metadata.
        [Fact]
        public void Execute_CreatesManifestForSymbols()
        {
            var expectedSymbolsPath = $"assets/symbols/{SymbolsName}";
            var expectedManifest =
$@"<Build Name=""{ManifestRepoUri}"" BuildId=""{ManifestBuildId}"" Branch=""{ManifestBranch}"" Commit=""{ManifestCommit}"" IsStable=""True"" Location=""{ManifestLocation}"">
  <Blob Id=""{expectedSymbolsPath}"" />
</Build>";
            var expectedLocation = $"{assetsTemporaryDirectory}/{SymbolsName}";

            var assetManifestPath = Path.Combine(
                manifestsDirectory,
                $"{nameof(Execute_CreatesManifestForSymbols)}.xml");
            var itemsToPush = new[]
            {
                new TaskItem(SymbolsPath),
            };

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                AssetManifestPath = assetManifestPath,
                AssetsTemporaryDirectory = assetsTemporaryDirectory,
                BuildEngine = buildEngine,
                IsStableBuild = true,
                ItemsToPush = itemsToPush,
                ManifestBranch = ManifestBranch,
                ManifestBuildData = ManifestBuildData,
                ManifestBuildId = ManifestBuildId,
                ManifestCommit = ManifestCommit,
                ManifestRepoUri = ManifestRepoUri,
            };

            var result = task.Execute();

            Assert.True(result);
            var assets = File.ReadAllText(assetManifestPath);
            Assert.Equal(expectedManifest, assets, ignoreLineEndingDifferences: true);

            var events = buildEngine.BuildMessageEvents;
            Assert.InRange(events.Count, 3, 4);

            var messageEvent = events[events.Count - 2];
            Assert.Contains("BlobArtifacts", messageEvent.Message);
            Assert.Contains(expectedLocation, messageEvent.Message);
        }

        [Fact]
        public void Execute_CreatesManifestForSymbols_WhenPublishFlatContainerTrue()
        {
            var relativeSymbolsPath = $"my/assets/{SymbolsName}";
            var expectedManifest =
$@"<Build Name=""{ManifestRepoUri}"" BuildId=""{ManifestBuildId}"" Branch=""{ManifestBranch}"" Commit=""{ManifestCommit}"" IsStable=""True"" Location=""{ManifestLocation}"">
  <Blob Id=""{relativeSymbolsPath}"" />
</Build>";
            var expectedLocation = $"{assetsTemporaryDirectory}/{SymbolsName}";

            var assetManifestPath = Path.Combine(
                manifestsDirectory,
                $"{nameof(Execute_CreatesManifestForSymbols_WhenPublishFlatContainerTrue)}.xml");
            var itemsToPush = new[]
            {
                new TaskItem(SymbolsPath),
            };
            itemsToPush[0].SetMetadata("RelativeBlobPath", relativeSymbolsPath);

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                AssetManifestPath = assetManifestPath,
                AssetsTemporaryDirectory = assetsTemporaryDirectory,
                BuildEngine = buildEngine,
                IsStableBuild = true,
                ItemsToPush = itemsToPush,
                ManifestBranch = ManifestBranch,
                ManifestBuildData = ManifestBuildData,
                ManifestBuildId = ManifestBuildId,
                ManifestCommit = ManifestCommit,
                ManifestRepoUri = ManifestRepoUri,
                PublishFlatContainer = true,
            };

            var result = task.Execute();

            Assert.True(result);
            var assets = File.ReadAllText(assetManifestPath);
            Assert.Equal(expectedManifest, assets, ignoreLineEndingDifferences: true);

            var events = buildEngine.BuildMessageEvents;
            Assert.InRange(events.Count, 3, 4);

            var messageEvent = events[events.Count - 2];
            Assert.Contains("BlobArtifacts", messageEvent.Message);
            Assert.Contains(expectedLocation, messageEvent.Message);
        }

        [Fact]
        public void Execute_CreatesManifestForThreeItems()
        {
            var expectedSymbolsPath = $"assets/symbols/{SymbolsName}";
            var relativeBlobPath = $"my/assets/{BlobName}";
            var expectedManifest =
$@"<Build Name=""{ManifestRepoUri}"" BuildId=""{ManifestBuildId}"" Branch=""{ManifestBranch}"" Commit=""{ManifestCommit}"" IsStable=""True"" Location=""{ManifestLocation}"">
  <Package Id=""{PackageId}"" Version=""{PackageVersion}"" />
  <Blob Id=""{expectedSymbolsPath}"" />
  <Blob Id=""{relativeBlobPath}"" />
</Build>";
            var expectedLocation = $"{assetsTemporaryDirectory}/{SymbolsName}";

            var assetManifestPath = Path.Combine(
                manifestsDirectory,
                $"{nameof(Execute_CreatesManifestForThreeItems)}.xml");
            var itemsToPush = new[]
            {
                new TaskItem(BlobPath),
                new TaskItem(PackagePath),
                new TaskItem(SymbolsPath),
            };
            itemsToPush[0].SetMetadata("PublishFlatContainer", "true");
            itemsToPush[0].SetMetadata("RelativeBlobPath", relativeBlobPath);

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                AssetManifestPath = assetManifestPath,
                AssetsTemporaryDirectory = assetsTemporaryDirectory,
                BuildEngine = buildEngine,
                IsStableBuild = true,
                ItemsToPush = itemsToPush,
                ManifestBranch = ManifestBranch,
                ManifestBuildData = ManifestBuildData,
                ManifestBuildId = ManifestBuildId,
                ManifestCommit = ManifestCommit,
                ManifestRepoUri = ManifestRepoUri,
            };

            var result = task.Execute();

            Assert.True(result);
            var assets = File.ReadAllText(assetManifestPath);
            Assert.Equal(expectedManifest, assets, ignoreLineEndingDifferences: true);

            var events = buildEngine.BuildMessageEvents;
            Assert.InRange(events.Count, 5, 6);
        }

        [Fact]
        public void Execute_CreatesManifestForThreeItems_WhenPublishFlatContainerTrue()
        {
            var relativeBlobPath = $"my/assets/{BlobName}";
            var relativePackagePath = $"my/assets/{PackageName}";
            var relativeSymbolsPath = $"my/assets/{SymbolsName}";
            var expectedManifest =
$@"<Build Name=""{ManifestRepoUri}"" BuildId=""{ManifestBuildId}"" Branch=""{ManifestBranch}"" Commit=""{ManifestCommit}"" IsStable=""True"" Location=""{ManifestLocation}"">
  <Blob Id=""{relativeBlobPath}"" />
  <Blob Id=""{relativePackagePath}"" />
  <Blob Id=""{relativeSymbolsPath}"" />
</Build>";
            var expectedLocation = $"{assetsTemporaryDirectory}/{SymbolsName}";

            var assetManifestPath = Path.Combine(
                manifestsDirectory,
                $"{nameof(Execute_CreatesManifestForThreeItems_WhenPublishFlatContainerTrue)}.xml");
            var itemsToPush = new[]
            {
                new TaskItem(BlobPath),
                new TaskItem(PackagePath),
                new TaskItem(SymbolsPath),
            };
            itemsToPush[0].SetMetadata("RelativeBlobPath", relativeBlobPath);
            itemsToPush[1].SetMetadata("RelativeBlobPath", relativePackagePath);
            itemsToPush[2].SetMetadata("RelativeBlobPath", relativeSymbolsPath);

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                AssetManifestPath = assetManifestPath,
                AssetsTemporaryDirectory = assetsTemporaryDirectory,
                BuildEngine = buildEngine,
                IsStableBuild = true,
                ItemsToPush = itemsToPush,
                ManifestBranch = ManifestBranch,
                ManifestBuildData = ManifestBuildData,
                ManifestBuildId = ManifestBuildId,
                ManifestCommit = ManifestCommit,
                ManifestRepoUri = ManifestRepoUri,
                PublishFlatContainer = true,
            };

            var result = task.Execute();

            Assert.True(result);
            var assets = File.ReadAllText(assetManifestPath);
            Assert.Equal(expectedManifest, assets, ignoreLineEndingDifferences: true);

            var events = buildEngine.BuildMessageEvents;
            Assert.InRange(events.Count, 5, 6);
        }

        [Fact]
        public void Execute_IgnoresItems_WithExcludeFromManifestMetadata()
        {
            var expectedManifest =
$@"<Build Name=""{ManifestRepoUri}"" BuildId=""{DefaultManifestBuildId}"" Branch=""{ManifestBranch}"" Commit=""{ManifestCommit}"" IsStable=""False"" Location=""{ManifestLocation}"" />";

            var assetManifestPath = Path.Combine(
                manifestsDirectory,
                $"{nameof(Execute_IgnoresItems_WithExcludeFromManifestMetadata)}.xml");
            var itemsToPush = new[]
            {
                new TaskItem(BlobPath),
                new TaskItem(PackagePath),
                new TaskItem(SymbolsPath),
            };
            itemsToPush[0].SetMetadata("ExcludeFromManifest", "true");
            itemsToPush[1].SetMetadata("ExcludeFromManifest", "true");
            itemsToPush[2].SetMetadata("ExcludeFromManifest", "true");

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                AssetManifestPath = assetManifestPath,
                AssetsTemporaryDirectory = assetsTemporaryDirectory,
                BuildEngine = buildEngine,
                ItemsToPush = itemsToPush,
                ManifestBranch = ManifestBranch,
                ManifestBuildData = ManifestBuildData,
                ManifestCommit = ManifestCommit,
                ManifestRepoUri = ManifestRepoUri,
            };

            var result = task.Execute();

            Assert.True(result);
            var assets = File.ReadAllText(assetManifestPath);
            Assert.Equal(expectedManifest, assets, ignoreLineEndingDifferences: true);

            var events = buildEngine.BuildMessageEvents;
            Assert.InRange(events.Count, 2, 3);
        }

        // Exactly the same results as Execute_IgnoresItems_WithExcludeFromManifestMetadata()
        [Fact]
        public void Execute_IgnoresItems_WithExcludeFromManifestMetadata_WhenPublishFlatContainerTrue()
        {
            var expectedManifest =
$@"<Build Name=""{ManifestRepoUri}"" BuildId=""{DefaultManifestBuildId}"" Branch=""{ManifestBranch}"" Commit=""{ManifestCommit}"" IsStable=""False"" Location=""{ManifestLocation}"" />";

            var assetManifestPath = Path.Combine(
                manifestsDirectory,
                $"{nameof(Execute_IgnoresItems_WithExcludeFromManifestMetadata_WhenPublishFlatContainerTrue)}.xml");
            var itemsToPush = new[]
            {
                new TaskItem(BlobPath),
                new TaskItem(PackagePath),
                new TaskItem(SymbolsPath),
            };
            itemsToPush[0].SetMetadata("ExcludeFromManifest", "true");
            itemsToPush[1].SetMetadata("ExcludeFromManifest", "true");
            itemsToPush[2].SetMetadata("ExcludeFromManifest", "true");

            var buildEngine = new MockBuildEngine();
            var task = new PushToAzureDevOpsArtifacts
            {
                AssetManifestPath = assetManifestPath,
                AssetsTemporaryDirectory = assetsTemporaryDirectory,
                BuildEngine = buildEngine,
                ItemsToPush = itemsToPush,
                ManifestBranch = ManifestBranch,
                ManifestBuildData = ManifestBuildData,
                ManifestCommit = ManifestCommit,
                ManifestRepoUri = ManifestRepoUri,
                PublishFlatContainer = true,
            };

            var result = task.Execute();

            Assert.True(result);
            var assets = File.ReadAllText(assetManifestPath);
            Assert.Equal(expectedManifest, assets, ignoreLineEndingDifferences: true);

            var events = buildEngine.BuildMessageEvents;
            Assert.InRange(events.Count, 2, 3);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(assetsTemporaryDirectory, recursive: true);
                Directory.Delete(manifestsDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors.
            }
        }
    }
}
