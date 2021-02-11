// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Arcade.Common;
using Microsoft.Arcade.Test.Common;
using Microsoft.DotNet.Internal.DependencyInjection.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Microsoft.DotNet.Arcade.Sdk.Tests
{
    public class InstallDotNetToolTests
    {
        private const string InstallPath = @"C:\dotnet_tools";
        private const string ToolName = "Microsoft.DotNet.Arcade.FakeTool";
        private const string ToolVersion = "1.0.0-prerelease.21108.1";
        private static readonly string s_installedPath = Path.Combine(InstallPath, ToolName, ToolVersion);

        private readonly Mock<IFileSystem> _fileSystemMock;
        private readonly Mock<IHelpers> _helpersMock;
        private readonly Mock<ICommandFactory> _commandFactoryMock;
        private readonly Mock<ICommand> _commandMock;

        private readonly InstallDotNetTool _task;

        private string _dotnetPath = "dotnet";
        private IEnumerable<string> _expectedArgs = new[]
        {
            "tool",
            "install",
            "--version",
            ToolVersion,
            "--tool-path",
            s_installedPath,
            ToolName,
        };

        public InstallDotNetToolTests()
        {
            _fileSystemMock = new Mock<IFileSystem>();
            _commandMock = new Mock<ICommand>();

            bool succeeded = false;

            _helpersMock = new Mock<IHelpers>();
            _helpersMock
                .Setup(x => x.DirectoryMutexExec(It.IsAny<Func<bool>>(), It.IsAny<string>()))
                .Callback<Func<bool>, string>((function, path) => {
                    succeeded = function();
                })
                .Returns(() => succeeded);

            _commandFactoryMock = new Mock<ICommandFactory>();
            _commandFactoryMock
                .Setup(x => x.Create(
                    It.Is<string>(s => s == _dotnetPath),
                    It.Is<IEnumerable<string>>(args => args.All(y => _expectedArgs.Contains(y)))))
                .Returns(_commandMock.Object)
                .Verifiable();

            _task = new InstallDotNetTool()
            {
                DestinationPath = InstallPath,
                Name = ToolName,
                Version = ToolVersion,
                BuildEngine = new MockBuildEngine(),
            };
        }

        [Fact]
        public void SkipsInstallation()
        {
            // Setup
            _fileSystemMock
                .Setup(x => x.DirectoryExists(It.IsAny<string>()))
                .Returns(true);

            var collection = CreateMockServiceCollection();
            _task.ConfigureServices(collection);

            // Act
            using var provider = collection.BuildServiceProvider();
            _task.InvokeExecute(provider).Should().BeTrue();

            // Verify
            _commandFactoryMock
                .Verify(
                    x => x.Create(
                        It.Is<string>(dotnet => dotnet == _dotnetPath),
                        It.Is<IEnumerable<string>>(args => args.Count() == _expectedArgs.Count() && args.All(y => _expectedArgs.Contains(y)))),
                    Times.Never);

            _commandMock.Verify(x => x.Execute(), Times.Never);
        }

        [Fact]
        public void InstallsToolSuccessfully()
        {
            // Setup
            _fileSystemMock
                .Setup(x => x.DirectoryExists(It.IsAny<string>()))
                .Returns(false);

            _commandMock
                .Setup(x => x.Execute())
                .Returns(new CommandResult(new ProcessStartInfo(), 0, "Tool installed", null));

            var collection = CreateMockServiceCollection();
            _task.ConfigureServices(collection);

            // Act
            using var provider = collection.BuildServiceProvider();
            _task.InvokeExecute(provider).Should().BeTrue();

            // Verify
            _commandFactoryMock.VerifyAll();
            _commandMock.Verify(x => x.Execute(), Times.Once);
            _fileSystemMock.Verify(x => x.CreateDirectory(InstallPath), Times.AtLeastOnce);
            _task.ToolPath.Should().Be(s_installedPath);
        }

        [Fact]
        public void TriesToInstallToolUnsuccessfully()
        {
            // Setup
            _fileSystemMock
                .Setup(x => x.DirectoryExists(It.IsAny<string>()))
                .Returns(false);

            _commandMock
                .Setup(x => x.Execute())
                .Returns(new CommandResult(new ProcessStartInfo(), 1, null, "Installation failed"));

            var collection = CreateMockServiceCollection();
            _task.ConfigureServices(collection);

            // Act
            using var provider = collection.BuildServiceProvider();
            _task.InvokeExecute(provider).Should().BeFalse();

            // Verify
            _commandFactoryMock.VerifyAll();
            _commandMock.Verify(x => x.Execute(), Times.Once);
            _fileSystemMock.Verify(x => x.CreateDirectory(InstallPath), Times.AtLeastOnce);
        }

        [Fact]
        public void InstallsToolWithExtraParamsSuccessfully()
        {
            // Setup
            _fileSystemMock
                .Setup(x => x.DirectoryExists(It.IsAny<string>()))
                .Returns(false);

            _commandMock
                .Setup(x => x.Execute())
                .Returns(new CommandResult(new ProcessStartInfo(), 0, "Tool installed", null));

            _expectedArgs = new[]
            {
                "tool",
                "install",
                "--version",
                ToolVersion,
                "--tool-path",
                Path.Combine(InstallPath, ToolName, ToolVersion),
                "--add-source",
                "https://dev.azure.com/some/feed",
                ToolName,
            };

            var collection = CreateMockServiceCollection();
            _task.ConfigureServices(collection);
            _task.Source = "https://dev.azure.com/some/feed";
            _task.DotnetPath = _dotnetPath = @"D:\dotnet\dotnet.exe";

            // Act
            using var provider = collection.BuildServiceProvider();
            _task.InvokeExecute(provider).Should().BeTrue();

            // Verify
            _commandFactoryMock.VerifyAll();
            _commandMock.Verify(x => x.Execute(), Times.Once);
            _fileSystemMock.Verify(x => x.CreateDirectory(InstallPath), Times.AtLeastOnce);
            _task.ToolPath.Should().Be(s_installedPath);
        }

        [Fact]
        public void AreDependenciesRegistered()
        {
            var task = new InstallDotNetTool();

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

        private IServiceCollection CreateMockServiceCollection()
        {
            var collection = new ServiceCollection();
            collection.TryAddSingleton(_commandFactoryMock.Object);
            collection.TryAddSingleton(_fileSystemMock.Object);
            collection.TryAddSingleton(_helpersMock.Object);
            return collection;
        }
    }
}
