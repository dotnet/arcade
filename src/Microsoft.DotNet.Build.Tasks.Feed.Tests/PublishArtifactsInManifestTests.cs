// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.Arcade.Common;
using Microsoft.Arcade.Test.Common;
using Microsoft.DotNet.Build.Manifest;
using Microsoft.DotNet.Build.Manifest.Tests;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
using Microsoft.DotNet.Internal.DependencyInjection.Testing;
using Microsoft.DotNet.ProductConstructionService.Client.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using static Microsoft.DotNet.Build.CloudTestTasks.AzureStorageUtils;
using static Microsoft.DotNet.Build.Tasks.Feed.PublishArtifactsInManifestBase;
using MsBuildUtils = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests;

public class PublishArtifactsInManifestTests
{
    private const string GeneralTestingChannelId = "529";
    private const string RandomToken = "abcd";
    private const string AzDOFeedUrl = "https://pkgs.dev.azure.com/dnceng/public/_packaging/a-dotnet-feed/nuget/v3/index.json";
    private const string StorageUrl = "https://dotnetstorageaccount.blob.core.windows.net/placewherethingsarepublished/index.json";

    /// <summary>
    /// Mock implementation of ITargetChannelValidator for testing negative scenarios.
    /// </summary>
    private class MockTargetChannelValidator : ITargetChannelValidator
    {
        private readonly TargetChannelValidationResult _validationResult;
        private readonly bool _shouldValidate;

        public MockTargetChannelValidator(TargetChannelValidationResult validationResult, bool shouldValidate = true)
        {
            _validationResult = validationResult;
            _shouldValidate = shouldValidate;
        }

        public int ValidateCallCount { get; private set; }
        public ProductConstructionService.Client.Models.Build LastBuild { get; private set; }
        public TargetChannelConfig LastTargetChannel { get; private set; }

        public Task<TargetChannelValidationResult> ValidateAsync(ProductConstructionService.Client.Models.Build build, TargetChannelConfig targetChannel)
        {
            if (_shouldValidate)
            {
                ValidateCallCount++;
                LastBuild = build;
                LastTargetChannel = targetChannel;
            }
            return Task.FromResult(_validationResult);
        }
    }

    /// <summary>
    /// Test publishing task that exposes the ValidateTargetChannelAsync method for testing.
    /// </summary>
    private class TestablePublishArtifactsTask : PublishArtifactsInManifestBase
    {
        public TestablePublishArtifactsTask(ITargetChannelValidator validator = null) 
            : base(null, validator)
        {
        }

        public override Task<bool> ExecuteAsync()
        {
            throw new NotImplementedException();
        }

        public new async Task<bool> ValidateTargetChannelAsync(
            ProductConstructionService.Client.Models.Build build, 
            TargetChannelConfig targetChannel)
        {
            return await base.ValidateTargetChannelAsync(build, targetChannel);
        }
    }

    private sealed class RecordingAssetPublisher : IAssetPublisher
    {
        private readonly TaskCompletionSource _started;
        private readonly Task _completion;

        public RecordingAssetPublisher(TaskCompletionSource started = null, Task completion = null)
        {
            _started = started;
            _completion = completion;
        }

        public List<string> PublishedBlobPaths { get; } = new();

        public LocationType LocationType => LocationType.Container;

        public async Task PublishAssetAsync(string file, string blobPath, PushOptions options, SemaphoreSlim clientThrottle = null)
        {
            lock (PublishedBlobPaths)
            {
                PublishedBlobPaths.Add(blobPath);
            }

            _started?.SetResult();
            if (_completion != null)
            {
                await _completion;
            }
        }
    }

    private sealed class RecordingAssetPublisherFactory : AssetPublisherFactory
    {
        private readonly RecordingAssetPublisher _publisher;

        public RecordingAssetPublisherFactory(RecordingAssetPublisher publisher)
            : base(null)
        {
            _publisher = publisher;
        }

        public List<TargetFeedConfig> FeedConfigs { get; } = new();

        public override IAssetPublisher CreateAssetPublisher(TargetFeedConfig feedConfig, PublishArtifactsInManifestBase task)
        {
            FeedConfigs.Add(feedConfig);
            return _publisher;
        }
    }

    private sealed class TestableBlobPublishingTask : PublishArtifactsInManifestBase
    {
        public TestableBlobPublishingTask(AssetPublisherFactory assetPublisherFactory)
            : base(assetPublisherFactory)
        {
        }

        public List<(HashSet<BlobArtifactModel> Blobs, TargetFeedConfig FeedConfig)> LatestLinkRequests { get; } = new();

        public override Task<bool> ExecuteAsync() => throw new NotImplementedException();

        public Task PublishBlobsAsync() => HandleBlobPublishingAsync(ReadOnlyDictionary<string, ProductConstructionService.Client.Models.Asset>.Empty);

        protected override bool TryAddAssetLocation(string assetId, string assetVersion, ReadOnlyDictionary<string, ProductConstructionService.Client.Models.Asset> buildAssets, TargetFeedConfig feedConfig, LocationType assetLocationType) => true;

        protected override Task CreateOrUpdateLatestLinksAsync(HashSet<BlobArtifactModel> blobAssets, TargetFeedConfig feedConfig)
        {
            LatestLinkRequests.Add((blobAssets, feedConfig));
            return Task.CompletedTask;
        }
    }

    private class TestablePublishArtifactsInManifest : PublishArtifactsInManifest
    {
        private readonly PublishArtifactsInManifestBase[] _publishingTasks;
        private int _publishingTaskIndex;

        public TestablePublishArtifactsInManifest(params PublishArtifactsInManifestBase[] publishingTasks)
        {
            _publishingTasks = publishingTasks;
        }

        public override PublishArtifactsInManifestBase WhichPublishingTask(string manifestFullPath)
        {
            if (_publishingTaskIndex >= _publishingTasks.Length)
            {
                throw new InvalidOperationException($"No publishing task was configured for manifest '{manifestFullPath}'.");
            }

            return _publishingTasks[_publishingTaskIndex++];
        }
    }

    private class DelegatePublishingTask : PublishArtifactsInManifestBase
    {
        private readonly Func<Task<bool>> _executeAsync;

        public DelegatePublishingTask(Func<Task<bool>> executeAsync)
        {
            _executeAsync = executeAsync;
        }

        public override Task<bool> ExecuteAsync()
        {
            return _executeAsync();
        }
    }

    /// <summary>
    /// Creates a test Build object with the required constructor parameters.
    /// </summary>
    private static ProductConstructionService.Client.Models.Build CreateTestBuild(
        int id = 12345, 
        DateTimeOffset? dateProduced = null,
        int staleness = 0,
        bool released = false,
        bool stable = false,
        string commit = "abc123",
        List<ProductConstructionService.Client.Models.Channel> channels = null,
        List<ProductConstructionService.Client.Models.Asset> assets = null,
        List<ProductConstructionService.Client.Models.BuildRef> dependencies = null,
        List<ProductConstructionService.Client.Models.BuildIncoherence> incoherencies = null)
    {
        return new ProductConstructionService.Client.Models.Build(
            id: id,
            dateProduced: dateProduced ?? DateTimeOffset.UtcNow,
            staleness: staleness,
            released: released,
            stable: stable,
            commit: commit,
            channels: channels ?? new List<ProductConstructionService.Client.Models.Channel>(),
            assets: assets ?? new List<ProductConstructionService.Client.Models.Asset>(),
            dependencies: dependencies ?? new List<ProductConstructionService.Client.Models.BuildRef>(),
            incoherencies: incoherencies ?? new List<ProductConstructionService.Client.Models.BuildIncoherence>()
        );
    }

    private static BlobArtifactModel CreateBlob(string id, bool nonShipping = false) => new()
    {
        Id = id,
        NonShipping = nonShipping
    };

    private static TargetFeedConfig CreateBlobFeedConfig(
        string targetUrl,
        string latestLinkPrefix,
        AssetSelection assetSelection = AssetSelection.All,
        bool allowOverwrite = false)
    {
        return new TargetFeedConfig(
            TargetFeedContentType.Other,
            targetUrl,
            FeedType.AzureStorageContainer,
            token: "token",
            latestLinkShortUrlPrefixes: [latestLinkPrefix],
            akaMSCreateLinkPatterns: [new Regex(".*")],
            assetSelection: assetSelection,
            allowOverwrite: allowOverwrite);
    }

    private static (TestableBlobPublishingTask Task, string BlobDirectory) CreateBlobPublishingTask(
        RecordingAssetPublisher publisher,
        IEnumerable<BlobArtifactModel> blobs,
        params TargetFeedConfig[] feedConfigs)
    {
        return CreateBlobPublishingTask(new RecordingAssetPublisherFactory(publisher), blobs, feedConfigs);
    }

    private static (TestableBlobPublishingTask Task, string BlobDirectory) CreateBlobPublishingTask(
        RecordingAssetPublisherFactory publisherFactory,
        IEnumerable<BlobArtifactModel> blobs,
        params TargetFeedConfig[] feedConfigs)
    {
        string blobDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(blobDirectory);
        foreach (var blob in blobs)
        {
            File.WriteAllText(Path.Combine(blobDirectory, Path.GetFileName(blob.Id)), "test");
        }

        var task = new TestableBlobPublishingTask(publisherFactory)
        {
            BlobAssetsBasePath = blobDirectory,
            BuildEngine = new MockBuildEngine(),
            NonStreamingPublishingMaxClients = 1
        };
        task.BlobsByCategory.Add(TargetFeedContentType.Other, blobs.ToHashSet());
        task.FeedConfigs.Add(TargetFeedContentType.Other, feedConfigs.ToHashSet());
        return (task, blobDirectory);
    }

    [Fact]
    public async Task HandleBlobPublishingAsyncDeduplicatesUploadsAndPreservesLatestLinkMappings()
    {
        var publisher = new RecordingAssetPublisher();
        var blob = CreateBlob("asset.zip");
        var firstConfig = CreateBlobFeedConfig("https://storage.example.net/public", "dotnet/first");
        var secondConfig = CreateBlobFeedConfig("https://storage.example.net/public", "dotnet/second");
        var (task, blobDirectory) = CreateBlobPublishingTask(publisher, [blob], firstConfig, secondConfig);

        try
        {
            await task.PublishBlobsAsync();

            publisher.PublishedBlobPaths.Should().ContainSingle().Which.Should().Be("asset.zip");
            task.LatestLinkRequests.Should().HaveCount(2);
            task.LatestLinkRequests.Select(request => request.FeedConfig).Should().BeEquivalentTo([firstConfig, secondConfig]);
        }
        finally
        {
            Directory.Delete(blobDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task HandleBlobPublishingAsyncBatchesDeduplicatedBlobsByUploadConfiguration()
    {
        var publisher = new RecordingAssetPublisher();
        var publisherFactory = new RecordingAssetPublisherFactory(publisher);
        var firstBlob = CreateBlob("first.zip");
        var secondBlob = CreateBlob("second.zip");
        var firstConfig = CreateBlobFeedConfig("https://storage.example.net/public", "dotnet/first");
        var secondConfig = CreateBlobFeedConfig("https://storage.example.net/public", "dotnet/second");
        var (task, blobDirectory) = CreateBlobPublishingTask(
            publisherFactory,
            [firstBlob, secondBlob],
            firstConfig,
            secondConfig);

        try
        {
            await task.PublishBlobsAsync();

            publisher.PublishedBlobPaths.Should().BeEquivalentTo([firstBlob.Id, secondBlob.Id]);
            publisherFactory.FeedConfigs.Should().ContainSingle();
            task.LatestLinkRequests.Should().HaveCount(2);
        }
        finally
        {
            Directory.Delete(blobDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task HandleBlobPublishingAsyncSelectsUploadConfigDeterministically()
    {
        var publisher = new RecordingAssetPublisher();
        var publisherFactory = new RecordingAssetPublisherFactory(publisher);
        var blob = CreateBlob("asset.zip");
        var configWithTrailingSlash = CreateBlobFeedConfig("https://storage.example.net/public/", "dotnet/first");
        var configWithoutTrailingSlash = CreateBlobFeedConfig("https://storage.example.net/public", "dotnet/second");
        var (task, blobDirectory) = CreateBlobPublishingTask(
            publisherFactory,
            [blob],
            configWithTrailingSlash,
            configWithoutTrailingSlash);

        try
        {
            await task.PublishBlobsAsync();

            publisher.PublishedBlobPaths.Should().ContainSingle();
            publisherFactory.FeedConfigs.Should().ContainSingle()
                .Which.TargetURL.Should().Be(configWithoutTrailingSlash.TargetURL);
        }
        finally
        {
            Directory.Delete(blobDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task HandleBlobPublishingAsyncRejectsConflictingTargetUrlQueries()
    {
        var publisher = new RecordingAssetPublisher();
        var blob = CreateBlob("asset.zip");
        var firstConfig = CreateBlobFeedConfig("https://storage.example.net/public?first", "dotnet/first");
        var secondConfig = CreateBlobFeedConfig("https://storage.example.net/public?second", "dotnet/second");
        var (task, blobDirectory) = CreateBlobPublishingTask(publisher, [blob], firstConfig, secondConfig);

        try
        {
            await task.PublishBlobsAsync();

            publisher.PublishedBlobPaths.Should().BeEmpty();
            task.LatestLinkRequests.Should().BeEmpty();
            task.Log.HasLoggedErrors.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(blobDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task HandleBlobPublishingAsyncPreservesLinksForMappingsWithoutConflicts()
    {
        var publisher = new RecordingAssetPublisher();
        var shippingBlob = CreateBlob("shipping.zip");
        var nonShippingBlob = CreateBlob("nonshipping.zip", nonShipping: true);
        var conflictingConfig = CreateBlobFeedConfig(
            "https://storage.example.net/conflict",
            "dotnet/conflict",
            AssetSelection.ShippingOnly);
        var conflictingOverwriteConfig = CreateBlobFeedConfig(
            "https://storage.example.net/conflict",
            "dotnet/conflict-overwrite",
            AssetSelection.ShippingOnly,
            allowOverwrite: true);
        var validConfig = CreateBlobFeedConfig(
            "https://storage.example.net/valid",
            "dotnet/valid",
            AssetSelection.NonShippingOnly);
        var (task, blobDirectory) = CreateBlobPublishingTask(
            publisher,
            [shippingBlob, nonShippingBlob],
            conflictingConfig,
            conflictingOverwriteConfig,
            validConfig);

        try
        {
            await task.PublishBlobsAsync();

            publisher.PublishedBlobPaths.Should().ContainSingle().Which.Should().Be(nonShippingBlob.Id);
            task.LatestLinkRequests.Should().ContainSingle();
            task.LatestLinkRequests[0].Blobs.Should().ContainSingle().Which.Should().Be(nonShippingBlob);
            task.LatestLinkRequests[0].FeedConfig.Should().Be(validConfig);
            task.Log.HasLoggedErrors.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(blobDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task HandleBlobPublishingAsyncIgnoresUnrelatedLoggedErrorsWhenCreatingLinks()
    {
        var publisher = new RecordingAssetPublisher();
        var blob = CreateBlob("asset.zip");
        var config = CreateBlobFeedConfig("https://storage.example.net/public", "dotnet/first");
        var (task, blobDirectory) = CreateBlobPublishingTask(publisher, [blob], config);
        task.Log.LogError("Unrelated publishing error");

        try
        {
            await task.PublishBlobsAsync();

            publisher.PublishedBlobPaths.Should().ContainSingle();
            task.LatestLinkRequests.Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(blobDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task HandleBlobPublishingAsyncDoesNotDeduplicateDifferentDestinationsOrBlobPaths()
    {
        var publisher = new RecordingAssetPublisher();
        var publisherFactory = new RecordingAssetPublisherFactory(publisher);
        var firstBlob = CreateBlob("first.zip");
        var secondBlob = CreateBlob("second.zip");
        var firstConfig = CreateBlobFeedConfig("https://storage.example.net/first", "dotnet/first");
        var secondConfig = CreateBlobFeedConfig("https://storage.example.net/second", "dotnet/second");
        var (task, blobDirectory) = CreateBlobPublishingTask(publisherFactory, [firstBlob, secondBlob], firstConfig, secondConfig);

        try
        {
            await task.PublishBlobsAsync();

            publisher.PublishedBlobPaths.Should().HaveCount(4);
            publisherFactory.FeedConfigs.Should().HaveCount(2);
        }
        finally
        {
            Directory.Delete(blobDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task HandleBlobPublishingAsyncDoesNotCollapseConflictingOverwriteSettings()
    {
        var publisher = new RecordingAssetPublisher();
        var blob = CreateBlob("asset.zip");
        var firstConfig = CreateBlobFeedConfig("https://storage.example.net/public", "dotnet/first");
        var secondConfig = CreateBlobFeedConfig("https://storage.example.net/public", "dotnet/second", allowOverwrite: true);
        var (task, blobDirectory) = CreateBlobPublishingTask(publisher, [blob], firstConfig, secondConfig);

        try
        {
            await task.PublishBlobsAsync();

            publisher.PublishedBlobPaths.Should().BeEmpty();
            task.Log.HasLoggedErrors.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(blobDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task HandleBlobPublishingAsyncFiltersBeforeDeduplicating()
    {
        var publisher = new RecordingAssetPublisher();
        var shippingBlob = CreateBlob("shipping.zip");
        var nonShippingBlob = CreateBlob("nonshipping.zip", nonShipping: true);
        var shippingConfig = CreateBlobFeedConfig("https://storage.example.net/public", "dotnet/shipping", AssetSelection.ShippingOnly);
        var nonShippingConfig = CreateBlobFeedConfig("https://storage.example.net/public", "dotnet/nonshipping", AssetSelection.NonShippingOnly);
        var (task, blobDirectory) = CreateBlobPublishingTask(publisher, [shippingBlob, nonShippingBlob], shippingConfig, nonShippingConfig);

        try
        {
            await task.PublishBlobsAsync();

            publisher.PublishedBlobPaths.Should().BeEquivalentTo(["shipping.zip", "nonshipping.zip"]);
            task.LatestLinkRequests.Should().HaveCount(2);
        }
        finally
        {
            Directory.Delete(blobDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task HandleBlobPublishingAsyncWaitsForAllUploadsBeforeCreatingLatestLinks()
    {
        var uploadStarted = new TaskCompletionSource();
        var completeUpload = new TaskCompletionSource();
        var publisher = new RecordingAssetPublisher(uploadStarted, completeUpload.Task);
        var blob = CreateBlob("asset.zip");
        var config = CreateBlobFeedConfig("https://storage.example.net/public", "dotnet/first");
        var (task, blobDirectory) = CreateBlobPublishingTask(publisher, [blob], config);

        try
        {
            Task publishTask = task.PublishBlobsAsync();
            await uploadStarted.Task;
            task.LatestLinkRequests.Should().BeEmpty();

            completeUpload.SetResult();
            await publishTask;
            task.LatestLinkRequests.Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(blobDirectory, recursive: true);
        }
    }

    // This test should be refactored: https://github.com/dotnet/arcade/issues/6715
    [Fact]
    public void ConstructV3PublishingTask()
    {
        var manifestFullPath = TestInputs.GetFullPath(Path.Combine("Manifests", "SampleV3.xml"));

        var buildEngine = new MockBuildEngine();
        var task = new PublishArtifactsInManifest()
        {
            BuildEngine = buildEngine,
            TargetChannels = GeneralTestingChannelId
        };

        // Dependency Injection setup
        var collection = new ServiceCollection()
            .AddSingleton<IFileSystem, FileSystem>()
            .AddSingleton<IBuildModelFactory, BuildModelFactory>();
        task.ConfigureServices(collection);
        using var provider = collection.BuildServiceProvider();

        // Act and Assert
        task.InvokeExecute(provider);

        var which = task.WhichPublishingTask(manifestFullPath);
        which.Should().BeOfType<PublishArtifactsInManifestV3>();
    }

    [Fact]
    public void ConstructV4PublishingTask()
    {
        var manifestFullPath = TestInputs.GetFullPath(Path.Combine("Manifests", "SampleV4.xml"));

        var buildEngine = new MockBuildEngine();
        var task = new PublishArtifactsInManifest()
        {
            BuildEngine = buildEngine,
            TargetChannels = GeneralTestingChannelId,
            AzdoApiToken = "test-token" // Add test token for DI
        };

        // Dependency Injection setup
        var collection = new ServiceCollection()
            .AddSingleton<IFileSystem, FileSystem>()
            .AddSingleton<IBuildModelFactory, BuildModelFactory>()
            .AddSingleton<ITargetChannelValidator, ProductionChannelValidator>()
            .AddSingleton<IProductionChannelValidatorBuildInfoService>(provider =>
            {
                var httpClient = provider.GetRequiredService<HttpClient>();
                var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<AzureDevOpsService>();
                return new AzureDevOpsService(httpClient, logger, "test-token");
            })
            .AddSingleton<IBranchClassificationService>(provider =>
            {
                var httpClient = provider.GetRequiredService<HttpClient>();
                var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<BranchClassificationService>();
                return new BranchClassificationService(httpClient, logger, "test-token");
            })
            .AddSingleton<HttpClient>()
            .AddLogging(); // Add logging services
        task.ConfigureServices(collection);
        using var provider = collection.BuildServiceProvider();

        // Act and Assert
        task.InvokeExecute(provider);

        var which = task.WhichPublishingTask(manifestFullPath);
        which.Should().BeOfType<PublishArtifactsInManifestV4>();
    }

    [Theory]
    [InlineData("SampleV3.xml")]
    [InlineData("SampleV4.xml")]
    public void ConstructPublishingTaskUsesInnerLoggerForAssetPublisherFactory(string manifestName)
    {
        var manifestFullPath = TestInputs.GetFullPath(Path.Combine("Manifests", manifestName));

        var buildEngine = new MockBuildEngine();
        var task = new PublishArtifactsInManifest()
        {
            BuildEngine = buildEngine,
            TargetChannels = GeneralTestingChannelId,
            AzdoApiToken = "test-token"
        };

        var collection = new ServiceCollection()
            .AddSingleton<IFileSystem, FileSystem>()
            .AddSingleton<IBuildModelFactory, BuildModelFactory>()
            .AddSingleton<ITargetChannelValidator, ProductionChannelValidator>()
            .AddSingleton<IProductionChannelValidatorBuildInfoService>(provider =>
            {
                var httpClient = provider.GetRequiredService<HttpClient>();
                var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<AzureDevOpsService>();
                return new AzureDevOpsService(httpClient, logger, "test-token");
            })
            .AddSingleton<IBranchClassificationService>(provider =>
            {
                var httpClient = provider.GetRequiredService<HttpClient>();
                var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<BranchClassificationService>();
                return new BranchClassificationService(httpClient, logger, "test-token");
            })
            .AddSingleton<HttpClient>()
            .AddLogging();
        task.ConfigureServices(collection);
        using var provider = collection.BuildServiceProvider();
        task.InvokeExecute(provider);

        var publishingTask = task.WhichPublishingTask(manifestFullPath);
        publishingTask.AssetPublisherFactory.Log.Should().BeSameAs(publishingTask.Log);
        publishingTask.AssetPublisherFactory.Log.Should().NotBeSameAs(task.Log);
    }

    [Fact]
    public async Task ConfigureServicesInjectsMSBuildLoggerIntoValidator()
    {
        var buildEngine = new MockBuildEngine();
        var task = new PublishArtifactsInManifest
        {
            BuildEngine = buildEngine
        };
        var collection = new ServiceCollection();
        task.ConfigureServices(collection);
        using var provider = collection.BuildServiceProvider();
        var validator = provider.GetRequiredService<ITargetChannelValidator>();
        var productionChannel = new TargetChannelConfig(
            id: 1,
            isInternal: false,
            publishingInfraVersion: PublishingInfraVersion.Latest,
            akaMSChannelNames: null,
            akaMSCreateLinkPatterns: null,
            akaMSDoNotCreateLinkPatterns: null,
            targetFeeds: new TargetFeedSpecification[0],
            symbolTargetType: SymbolPublishVisibility.None,
            flatten: true,
            isProduction: true);

        await validator.ValidateAsync(CreateTestBuild(), productionChannel);

        buildEngine.BuildMessageEvents.Should().Contain(message =>
            message.Importance == Microsoft.Build.Framework.MessageImportance.High &&
            message.Message == "Validating build 12345 for production channel 1");
    }

    [Fact]
    public async Task ExecuteAsyncReturnsFalseAndDoesNotPromoteWhenOuterLoggerHasErrors()
    {
        var buildEngine = new MockBuildEngine();
        TestablePublishArtifactsInManifest task = null;
        var publishingTask = new DelegatePublishingTask(() =>
        {
            task.Log.LogError("Asset 'asset.zip' already exists with different contents at 'https://example.test/asset.zip'");
            return Task.FromResult(true);
        });

        task = new TestablePublishArtifactsInManifest(publishingTask)
        {
            BuildEngine = buildEngine,
            AssetManifestPaths = new[] { new MsBuildUtils.TaskItem("manifest.xml") },
            BARBuildId = 123,
            MaestroApiEndpoint = "https://maestro.example.test",
            TargetChannels = GeneralTestingChannelId
        };

        bool result = await task.ExecuteAsync();

        result.Should().BeFalse();
        buildEngine.BuildErrorEvents.Should().ContainSingle(error =>
            error.Message.Contains("already exists with different contents"));
    }

    [Theory]
    [InlineData(TargetChannelValidationResult.Success)]
    [InlineData(TargetChannelValidationResult.Fail)]
    public async Task ValidateTargetChannelAsync_ProductionChannelValidation_Works(TargetChannelValidationResult validationResult)
    {
        // Arrange
        var mockValidator = new MockTargetChannelValidator(validationResult: validationResult);
        var task = new TestablePublishArtifactsTask(mockValidator);
        var buildEngine = new MockBuildEngine();
        task.BuildEngine = buildEngine;

        var build = CreateTestBuild(id: 12345, commit: "abc123");

        var productionChannel = new TargetChannelConfig(
            id: 1,
            isInternal: false,
            publishingInfraVersion: PublishingInfraVersion.Latest,
            akaMSChannelNames: null,
            akaMSCreateLinkPatterns: null,
            akaMSDoNotCreateLinkPatterns: null,
            targetFeeds: new TargetFeedSpecification[0],
            symbolTargetType: SymbolPublishVisibility.None,
            flatten: true,
            isProduction: true);

        // Act
        var result = await task.ValidateTargetChannelAsync(build, productionChannel);

        // Assert
        // For both Success and AuditOnlyFailure, the method returns true (allows publishing)
        // Only Fail should return false
        bool expectedResult = validationResult != TargetChannelValidationResult.Fail;
        result.Should().Be(expectedResult);
        
        mockValidator.ValidateCallCount.Should().Be(1);
        mockValidator.LastBuild.Should().Be(build);
        mockValidator.LastTargetChannel.Should().Be(productionChannel);
        
        // Check that validation log message was written
        buildEngine.BuildMessageEvents.Should().Contain(m => 
            m.Importance == Microsoft.Build.Framework.MessageImportance.Normal &&
            m.Message.Contains("Validating production channel 1"));

        if (validationResult == TargetChannelValidationResult.Fail)
        {
            // Check that error was logged
            buildEngine.BuildErrorEvents.Should().Contain(error =>
                error.Message.Contains("Build validation failed for production channel 1"));
        }
        else if (validationResult == TargetChannelValidationResult.AuditOnlyFailure)
        {
            // Check that warning was logged for audit-only failure
            buildEngine.BuildWarningEvents.Should().Contain(warning =>
                warning.Message.Contains("Build validation audit failure for production channel 1"));
        }
        else
        {
            // Check that no error was logged for success
            buildEngine.BuildErrorEvents.Should().BeEmpty();
        }
    }

    [Theory]
    [InlineData("https://pkgs.dev.azure.com/dnceng/public/_packaging/mmitche-test-transport/nuget/v3/index.json", "dnceng", "public/", "mmitche-test-transport")]
    [InlineData("https://pkgs.dev.azure.com/DevDiv/public/_packaging/1234.5/nuget/v3/index.json", "DevDiv", "public/", "1234.5")]
    [InlineData("https://pkgs.dev.azure.com/DevDiv/_packaging/1234.5/nuget/v3/index.json", "DevDiv", "", "1234.5")]
    public void NugetFeedParseTests(string uri, string account, string visibility, string feed)
    {
        var matches = Regex.Match(uri, PublishingConstants.AzDoNuGetFeedPattern);
        matches.Groups["account"]?.Value.Should().Be(account);
        matches.Groups["visibility"]?.Value.Should().Be(visibility);
        matches.Groups["feed"]?.Value.Should().Be(feed);
    }


    [Theory]
    // Test cases:
    // Succeeds on first try, not already on feed
    [InlineData(1, false, true)]
    // Succeeds on second try, turns out to be already on the feed
    [InlineData(2, true, true)]
    // Succeeds on last possible try (for retry logic)
    [InlineData(5, false, true)]
    // Succeeds by determining streams match and takes no action.
    [InlineData(1, true, true)]
    // Fails due to too many retries
    [InlineData(7, false, true, true)]
    // Fails and gives up due to non-matching streams (CompareLocalPackageToFeedPackage says no match)
    [InlineData(10, true, false, true)]
    public async Task PushNugetPackageTestsAsync(int pushAttemptsBeforeSuccess, bool packageAlreadyOnFeed,  bool localPackageMatchesFeed, bool expectedFailure = false)
    {
        // Setup
        var buildEngine = new MockBuildEngine();
        int timesCalled = 0;
        var testPackagePath = TestInputs.GetFullPath(Path.Combine("Nupkgs", "test-package-a.1.0.0.nupkg"));

        // Functionality is the same as this is in the base class, create a v2 object to test. 
        var task = new PublishArtifactsInManifestV3
        {
            InternalBuild = true,
            BuildEngine = buildEngine,
            MaxRetryCount = 5,
            RetryHandler = new ExponentialRetry
            {
                MaxAttempts = 5,
                DelayBase = 1
            }
        };
        TargetFeedConfig config = new TargetFeedConfig(TargetFeedContentType.Package, "testUrl", FeedType.AzDoNugetFeed, "tokenValue");

        Func<string, string, HttpClient, MsBuildUtils.TaskLoggingHelper, Task<PackageFeedStatus>> testCompareLocalPackage = async (string localPackageFullPath, string packageContentUrl, HttpClient client, MsBuildUtils.TaskLoggingHelper log) =>
        {
            await (Task.Delay(10)); // To make this actually async
            Debug.WriteLine($"Called mocked CompareLocalPackageToFeedPackage() :  localPackageFullPath = {localPackageFullPath}, packageContentUrl = {packageContentUrl}");
            if (packageAlreadyOnFeed)
            {
                return localPackageMatchesFeed ? PackageFeedStatus.ExistsAndIdenticalToLocal : PackageFeedStatus.ExistsAndDifferent;
            }
            else
            {
                return PackageFeedStatus.DoesNotExist;
            }
        };

        Func<HttpClient, string, string, Stream, Task<NuGetFeedUploadPackageResult>> testPush = (_, feedName, feedUri, _) =>
        {
            Debug.WriteLine($"Called test push for {feedName}");
            timesCalled++;
            if (timesCalled >= pushAttemptsBeforeSuccess)
            {
                return Task.FromResult(NuGetFeedUploadPackageResult.Success);
            }
            else
            {
                return Task.FromResult(NuGetFeedUploadPackageResult.AlreadyExists);
            }
        };

        await task.PushNugetPackageAsync(
            config, 
            null,
            testPackagePath, 
            "1234", 
            "version", 
            "feedaccount", 
            "feedvisibility", 
            "feedname",
            testCompareLocalPackage,
            testPush);
        if (!expectedFailure && localPackageMatchesFeed)
        {
            // Successful retry scenario; make sure we ran the # of retries we thought.
            timesCalled.Should().BeLessThanOrEqualTo(task.MaxRetryCount);
        }
        expectedFailure.Should().Be(task.Log.HasLoggedErrors);
    }

    [Fact]
    public async Task NuGetFeedUploadPackageAsyncLogsFailedResponse()
    {
        var buildEngine = new MockBuildEngine();
        var task = new PublishArtifactsInManifestV3
        {
            BuildEngine = buildEngine
        };
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            ReasonPhrase = "Package Rejected",
            Content = new StringContent("The package metadata is invalid.", Encoding.UTF8, "text/plain")
        };
        using HttpClient client = FakeHttpClient.WithResponses(response);
        using var packageStream = new MemoryStream([1, 2, 3]);

        NuGetFeedUploadPackageResult result = await NuGetFeedUploadPackageAsync(
            client,
            "test-feed",
            "https://pkgs.dev.azure.com/dnceng/_packaging/test-feed/nuget/v2",
            packageStream,
            task.Log);

        result.Should().Be(NuGetFeedUploadPackageResult.Failed);
        buildEngine.BuildMessageEvents.Should().Contain(message =>
            message.Message.Contains("HTTP 400 (Package Rejected)") &&
            message.Message.Contains("The package metadata is invalid."));
        buildEngine.BuildErrorEvents.Should().BeEmpty();
        buildEngine.BuildWarningEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task NuGetFeedUploadPackageAsyncLogsRequestException()
    {
        var buildEngine = new MockBuildEngine();
        var task = new PublishArtifactsInManifestV3
        {
            BuildEngine = buildEngine
        };
        using var client = new HttpClient(new ThrowingHttpMessageHandler());
        using var packageStream = new MemoryStream([1, 2, 3]);

        NuGetFeedUploadPackageResult result = await NuGetFeedUploadPackageAsync(
            client,
            "test-feed",
            "https://pkgs.dev.azure.com/dnceng/_packaging/test-feed/nuget/v2",
            packageStream,
            task.Log);

        result.Should().Be(NuGetFeedUploadPackageResult.Failed);
        buildEngine.BuildMessageEvents.Should().Contain(message =>
            message.Message.Contains("HttpRequestException") &&
            message.Message.Contains("Connection reset by peer."));
        buildEngine.BuildErrorEvents.Should().BeEmpty();
        buildEngine.BuildWarningEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task NuGetFeedUploadPackageAsyncBoundsLoggedResponseBody()
    {
        const string omittedText = "This text should not be logged.";
        var buildEngine = new MockBuildEngine();
        var task = new PublishArtifactsInManifestV3
        {
            BuildEngine = buildEngine
        };
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(new string('a', 4096) + omittedText)
        };
        using HttpClient client = FakeHttpClient.WithResponses(response);
        using var packageStream = new MemoryStream([1, 2, 3]);

        NuGetFeedUploadPackageResult result = await NuGetFeedUploadPackageAsync(
            client,
            "test-feed",
            "https://pkgs.dev.azure.com/dnceng/_packaging/test-feed/nuget/v2",
            packageStream,
            task.Log);

        result.Should().Be(NuGetFeedUploadPackageResult.Failed);
        buildEngine.BuildMessageEvents.Should().Contain(message =>
            message.Message.Contains(new string('a', 4096) + " [truncated]") &&
            !message.Message.Contains(omittedText));
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Connection reset by peer.");
        }
    }

    [Fact]
    public async Task PushNugetPackageUsesExponentialRetryForFailedUploads()
    {
        var buildEngine = new MockBuildEngine();
        var retryDelays = new List<TimeSpan>();
        var task = new PublishArtifactsInManifestV3
        {
            InternalBuild = true,
            BuildEngine = buildEngine,
            MaxRetryCount = 3,
            RetryHandler = new ExponentialRetry
            {
                MaxAttempts = 3,
                DelayBase = 1,
                RetryDelayCallback = (_, delay) => retryDelays.Add(delay)
            }
        };
        var config = new TargetFeedConfig(TargetFeedContentType.Package, "testUrl", FeedType.AzDoNugetFeed, "tokenValue");
        var testPackagePath = TestInputs.GetFullPath(Path.Combine("Nupkgs", "test-package-a.1.0.0.nupkg"));
        int attempts = 0;

        await task.PushNugetPackageAsync(
            config,
            null,
            testPackagePath,
            "1234",
            "version",
            "feedaccount",
            "feedvisibility",
            "feedname",
            AttemptPushPackageCallback: (_, _, _, _) =>
            {
                attempts++;
                return Task.FromResult(attempts == 3
                    ? NuGetFeedUploadPackageResult.Success
                    : NuGetFeedUploadPackageResult.Failed);
            });

        attempts.Should().Be(3);
        retryDelays.Should().HaveCount(2);
        buildEngine.BuildErrorEvents.Should().BeEmpty();
    }


    [Theory]
    // Simple case where we fill the whole buffer on each stream call and the streams match
    [InlineData("QXJjYWRl", "QXJjYWRl", new int[] { int.MaxValue }, new int[] { int.MaxValue }, 1024)]
    // Simple case where we fill the whole buffer on each stream call and the streams don't match
    [InlineData("QXJjYWRl", "QXJjYWRm", new int[] { int.MaxValue }, new int[] { int.MaxValue }, 1024)]
    // Case where the first stream returns everything initially, but the second returns one byte at a time.
    [InlineData("QXJjYWRl", "QXJjYWRl", new int[] { int.MaxValue }, new int[] { 1, 1, 1, 1, 1 }, 1024)]
    // Case where the first stream returns everything initially, but the second returns one byte at a time and they are not equal
    [InlineData("QXJjYWRl", "QXJjYWRm", new int[] { int.MaxValue }, new int[] { 1, 1, 1, 1, 1 }, 1024)]
    // Case where both streams return one byte at a time
    [InlineData("QXJjYWRl", "QXJjYWRl", new int[] { 1, 1, 1, 1, 1 }, new int[] { 1, 1, 1, 1, 1 }, 1024)]
    // Case where both streams return one byte at a time
    [InlineData("QXJjYWRl", "QXJjYWQ=", new int[] { 1, 1, 1, 1, 1 }, new int[] { 1, 1, 1, 1, 1 }, 1024)]
    // Case where the buffer must wrap around and one stream returns faster than the other, equal streams
    [InlineData("VGhlIHF1aWNrIGJyb3JuIGZveCBqdW1wcyBvdmVyIHRoZSBsYXp5aXNoIGRvZ2dv", "VGhlIHF1aWNrIGJyb3JuIGZveCBqdW1wcyBvdmVyIHRoZSBsYXp5aXNoIGRvZ2dv", new int[] { 16, 16, 16, 16, 16, 16 }, new int[] { 1, 1, 1, 1, 1 }, 8)]
    // Case where the buffer must wrap around and one stream returns faster than the other, unequal streams
    [InlineData("VGhpcyBpcyBhIHNlbnRlbmNlIHRoYXQgaXMgYSBsaXR0bGUgbG9uZ2Vy", "VGhpcyBpcyBhIHNlbnRlbmNlIHRoYXQgaXMgYSBsb25nZXI=", new int[] { 7, 3, 5, 16, 16, 16 }, new int[] { 1, 1, 1, 1, 1 }, 8)]
    public async Task StreamComparisonTestsAsync(string streamA, string streamB, int[] maxStreamABytesReturnedEachCall, int[] maxStreamBBytesReturnedEachCall, int bufferSize)
    {
        byte[] streamABytes = Convert.FromBase64String(streamA);
        byte[] streamBBytes = Convert.FromBase64String(streamB);

        FakeStream fakeStreamA = new FakeStream(streamABytes, maxStreamABytesReturnedEachCall);
        FakeStream fakeStreamB = new FakeStream(streamBBytes, maxStreamBBytesReturnedEachCall);

        bool streamsShouldBeSame = streamA == streamB;
        bool streamsAreSame = await GeneralUtils.CompareStreamsAsync(fakeStreamA, fakeStreamB, bufferSize);
        streamsAreSame.Should().Be(streamsShouldBeSame, "Stream comparison failed");
    }

    class FakeStream : Stream
    {
        public FakeStream(byte[] streamBytes, int[] maxStreamBytesReturned)
        {
            _streamBytes = streamBytes;
            _maxStreamBytesReturned = maxStreamBytesReturned;
        }

        byte[] _streamBytes;
        int[] _maxStreamBytesReturned;
        int _callIndex = 0;
        int _position = 0;

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            count.Should().BeGreaterThan(0);

            // If we reach the end of the _maxStreamBytesReturned array, just use max int.
            int maxStreamBytesThisCall = int.MaxValue;
            if (_callIndex < _maxStreamBytesReturned.Length)
            {
                maxStreamBytesThisCall = _maxStreamBytesReturned[_callIndex];
                _callIndex++;
            }
            int bytesToWrite = Math.Min(Math.Min(_streamBytes.Length - _position, count), maxStreamBytesThisCall);

            for (int i = 0; i < bytesToWrite; i++)
            {
                buffer[offset + i] = _streamBytes[_position + i];
            }
            _position += bytesToWrite;

            return Task.FromResult(bytesToWrite);
        }

        #region Unused

        public override bool CanRead => throw new NotImplementedException();

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => throw new NotImplementedException();

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
        #endregion
    }

    [Fact]
    public void AreDependenciesRegistered()
    {
        PublishArtifactsInManifest task = new PublishArtifactsInManifest();

        var collection = new ServiceCollection();
        task.ConfigureServices(collection);
        var provider = collection.BuildServiceProvider();

        DependencyInjectionValidation.IsDependencyResolutionCoherent(
                s =>
                {
                    task.ConfigureServices(s);
                },
                out string message,
                additionalSingletonTypes: task.GetExecuteParameterTypes()
            )
            .Should()
            .BeTrue(message);
    }        
}
