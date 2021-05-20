// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Arcade.Common;
using Microsoft.Arcade.Test.Common;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Internal.DependencyInjection.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

#nullable enable
namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class CreateXHarnessAppleWorkItemsTests
    {
        private readonly MockFileSystem _fileSystem;
        private readonly Mock<IProvisioningProfileProvider> _profileProvider;
        private readonly Mock<IZipArchiveManager> _zipArchiveManager;
        private readonly CreateXHarnessAppleWorkItems _task;

        public CreateXHarnessAppleWorkItemsTests()
        {
            _fileSystem = new MockFileSystem();
            _profileProvider = new();
            _zipArchiveManager = new();
            _zipArchiveManager.SetReturnsDefault(Task.CompletedTask);
            _zipArchiveManager
                .Setup(x => x.ArchiveDirectory(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Callback<string, string, bool>((folder, zipPath, _) =>
                {
                    _fileSystem.Files.Add(zipPath, "zip of " + folder);
                });

            _task = new CreateXHarnessAppleWorkItems()
            {                
                BuildEngine = new MockBuildEngine(),
            };
        }

        [Fact]
        public void MissingTargetsPropertyIsCaught()
        {
            var collection = CreateMockServiceCollection();
            _task.ConfigureServices(collection);
            _task.AppBundles = new[]
            {
                CreateAppBundle("/apps/System.Foo.app", null!)
            };

            // Act
            using var provider = collection.BuildServiceProvider();
            _task.InvokeExecute(provider).Should().BeFalse();

            // Verify
            _task.WorkItems.Length.Should().Be(0);
        }

        [Fact]
        public void AppleXHarnessWorkItemIsCreated()
        {
            var collection = CreateMockServiceCollection();
            _task.ConfigureServices(collection);
            _task.AppBundles = new[]
            {
                CreateAppBundle("/apps/System.Foo.app", "ios-device_13.5", "00:15:42", "00:08:55", "00:02:33")
            };
            _task.TmpDir = "/tmp";

            // Act
            using var provider = collection.BuildServiceProvider();
            _task.InvokeExecute(provider).Should().BeTrue();

            // Verify
            _task.WorkItems.Length.Should().Be(1);

            var workItem = _task.WorkItems.First();
            workItem.GetMetadata("Identity").Should().Be("System.Foo");
            workItem.GetMetadata("Timeout").Should().Be("00:15:42");

            var payloadArchive = workItem.GetMetadata("PayloadArchive");
            payloadArchive.Should().NotBeNullOrEmpty();
            _fileSystem.FileExists(payloadArchive).Should().BeTrue();

            var command = workItem.GetMetadata("Command");
            command.Should().Contain("System.Foo.app");
            command.Should().Contain("--targets \"ios-device_13.5\"");
            command.Should().Contain("--timeout \"00:08:55\"");
            command.Should().Contain("--launch-timeout \"00:02:33\"");

            _profileProvider
                .Verify(x => x.AddProfilesToBundles(It.Is<ITaskItem[]>(bundles => bundles.Any(b => b.ItemSpec == "/apps/System.Foo.app"))), Times.Once);
            _zipArchiveManager
                .Verify(x => x.AddResourceFileToArchive<CreateXHarnessAppleWorkItems>(payloadArchive, It.Is<string>(s => s.Contains("xharness-helix-job.apple.sh")), "xharness-helix-job.apple.sh"), Times.Once);
            _zipArchiveManager
                .Verify(x => x.AddResourceFileToArchive<CreateXHarnessAppleWorkItems>(payloadArchive, It.Is<string>(s => s.Contains("xharness-runner.apple.sh")), "xharness-runner.apple.sh"), Times.Once);
            _zipArchiveManager
                .Verify(x => x.AddContentToArchive(payloadArchive, "command.sh", It.Is<string>(s => s.Contains("xharness apple test"))), Times.Once);
        }

        [Fact]
        public void ArchivePayloadIsOverwritten()
        {
            var collection = CreateMockServiceCollection();
            _task.ConfigureServices(collection);
            _task.AppBundles = new[]
            {
                CreateAppBundle("apps/System.Foo.app", "ios-simulator-64_13.5"),
                CreateAppBundle("apps/System.Bar.app", "ios-simulator-64_13.5"),
            };

            _fileSystem.Files.Add("apps/xharness-app-payload-system.foo.app.zip", "archive");

            // Act
            using var provider = collection.BuildServiceProvider();
            _task.InvokeExecute(provider).Should().BeTrue();

            // Verify
            _task.WorkItems.Length.Should().Be(2);

            var workItem = _task.WorkItems.Last();
            workItem.GetMetadata("Identity").Should().Be("System.Bar");
            
            workItem = _task.WorkItems.First();
            workItem.GetMetadata("Identity").Should().Be("System.Foo");

            var payloadArchive = workItem.GetMetadata("PayloadArchive");
            payloadArchive.Should().NotBeNullOrEmpty();
            _fileSystem.FileExists(payloadArchive).Should().BeTrue();
            _fileSystem.RemovedFiles.Should().Contain(payloadArchive);
        }

        [Fact]
        public void CustomCommandsAreExecuted()
        {
            var collection = CreateMockServiceCollection();
            _task.ConfigureServices(collection);
            _task.AppBundles = new[]
            {
                CreateAppBundle("apps/System.Foo.app", "ios-simulator-64_13.5", customCommands: "echo foo"),
            };

            // Act
            using var provider = collection.BuildServiceProvider();
            _task.InvokeExecute(provider).Should().BeTrue();

            // Verify
            _task.WorkItems.Length.Should().Be(1);

            var workItem = _task.WorkItems.First();
            workItem.GetMetadata("Identity").Should().Be("System.Foo");

            var payloadArchive = workItem.GetMetadata("PayloadArchive");
            payloadArchive.Should().NotBeNullOrEmpty();
            _fileSystem.FileExists(payloadArchive).Should().BeTrue();

            _zipArchiveManager
                .Verify(x => x.AddContentToArchive(payloadArchive, "command.sh", "echo foo"), Times.Once);
        }

        [Fact]
        public void AreDependenciesRegistered()
        {
            var task = new CreateXHarnessAppleWorkItems();

            var collection = new ServiceCollection();
            task.ConfigureServices(collection);
            var provider = collection.BuildServiceProvider();

            DependencyInjectionValidation.IsDependencyResolutionCoherent(
                s => task.ConfigureServices(s),
                out string message,
                additionalSingletonTypes: task.GetExecuteParameterTypes()
            )
            .Should()
            .BeTrue(message);
        }

        private ITaskItem CreateAppBundle(
            string path,
            string targets,
            string? workItemTimeout = null,
            string? testTimeout = null,
            string? launchTimeout = null,
            int expectedExitCode = 0,
            bool includesTestRunner = true,
            string? customCommands = null)
        {
            var mockBundle = new Mock<ITaskItem>();
            mockBundle.SetupGet(x => x.ItemSpec).Returns(path);
            mockBundle.Setup(x => x.GetMetadata(CreateXHarnessAppleWorkItems.MetadataNames.Targets)).Returns(targets);
            mockBundle.Setup(x => x.GetMetadata(CreateXHarnessAppleWorkItems.MetadataNames.IncludesTestRunner)).Returns(includesTestRunner.ToString());

            if (workItemTimeout != null)
            {
                mockBundle.Setup(x => x.GetMetadata(XHarnessTaskBase.MetadataName.WorkItemTimeout)).Returns(workItemTimeout);
            }

            if (testTimeout != null)
            {
                mockBundle.Setup(x => x.GetMetadata(XHarnessTaskBase.MetadataName.TestTimeout)).Returns(testTimeout);
            }

            if (launchTimeout != null)
            {
                mockBundle.Setup(x => x.GetMetadata(CreateXHarnessAppleWorkItems.MetadataNames.LaunchTimeout)).Returns(launchTimeout);
            }

            if (expectedExitCode != 0)
            {
                mockBundle.Setup(x => x.GetMetadata(XHarnessTaskBase.MetadataName.ExpectedExitCode)).Returns(expectedExitCode.ToString());
            }

            if (customCommands != null)
            {
                mockBundle.Setup(x => x.GetMetadata(XHarnessTaskBase.MetadataName.CustomCommands)).Returns(customCommands);
            }

            _fileSystem.CreateDirectory(path);

            return mockBundle.Object;
        }

        private IServiceCollection CreateMockServiceCollection()
        {
            var collection = new ServiceCollection();
            collection.AddSingleton<IFileSystem>(_fileSystem);
            collection.AddSingleton(_profileProvider.Object);
            collection.AddSingleton(_zipArchiveManager.Object);
            return collection;
        }
    }
}
