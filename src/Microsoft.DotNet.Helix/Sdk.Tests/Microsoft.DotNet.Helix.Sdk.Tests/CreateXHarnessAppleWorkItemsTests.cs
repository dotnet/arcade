using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Arcade.Common;
using Microsoft.Arcade.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Microsoft.DotNet.Arcade.Test.Common;
using Microsoft.DotNet.Internal.DependencyInjection.Testing;
using Microsoft.Build.Framework;

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
        public void AppBundleMetadataParsedCorrectly()
        {
            var collection = CreateMockServiceCollection();
            _task.ConfigureServices(collection);
            _task.AppBundles = new[]
            {
                CreateAppBundle("/apps/System.Foo.app", "ios-simulator-64_13.5", "00:15:42")
            };

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
        }

        [Fact]
        public void AreDependenciesRegistered()
        {
            var task = new CreateXHarnessAppleWorkItems();

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

        private ITaskItem CreateAppBundle(
            string path,
            string targets,
            string? workItemTimeout = null,
            string? testTimeout = null,
            string? launchTimeout = null,
            int expectedExitCode = 0)
        {
            var mockBundle = new Mock<ITaskItem>();
            mockBundle.SetupGet(x => x.ItemSpec).Returns(path);
            mockBundle.Setup(x => x.GetMetadata(CreateXHarnessAppleWorkItems.TargetPropName)).Returns(targets);

            if (workItemTimeout != null)
            {
                mockBundle.Setup(x => x.GetMetadata("WorkItemTimeout")).Returns(workItemTimeout);
            }

            if (testTimeout != null)
            {
                mockBundle.Setup(x => x.GetMetadata("TestTimeout")).Returns(testTimeout);
            }

            if (launchTimeout != null)
            {
                mockBundle.Setup(x => x.GetMetadata("LaunchTimeout")).Returns(launchTimeout);
            }

            if (expectedExitCode != 0)
            {
                mockBundle.Setup(x => x.GetMetadata("ExpectedExitCode")).Returns(expectedExitCode.ToString());
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
