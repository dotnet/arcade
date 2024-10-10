// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests.AppOperations;

public class AppUninstallerTests
{
    private const string DeviceName = "Test iPad";
    private const string AppBundleId = "some.bundle.name.app";
    private const string UDID = "8A450AA31EA94191AD6B02455F377CC1";

    private readonly Mock<IMlaunchProcessManager> _processManager;
    private readonly Mock<ILog> _mainLog;
    private readonly AppUninstaller _appUninstaller;

    public AppUninstallerTests()
    {
        _mainLog = new Mock<ILog>();

        _processManager = new Mock<IMlaunchProcessManager>();
        _processManager.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult() { ExitCode = 0 }));

        _appUninstaller = new AppUninstaller(_processManager.Object, _mainLog.Object);
    }

    [Fact]
    public async Task UninstallFromSimulatorTest()
    {
        var simulator = Mock.Of<ISimulatorDevice>(x => x.Name == DeviceName && x.UDID == UDID);

        // Act
        var result = await _appUninstaller.UninstallSimulatorApp(simulator, AppBundleId);

        // Verify
        Assert.Equal(0, result.ExitCode);

        var expectedArgs = $"uninstall {UDID} {StringUtils.FormatArguments(AppBundleId)}";

        _processManager.Verify(x => x.ExecuteXcodeCommandAsync(
            "simctl",
           It.Is<string[]>(args => string.Join(" ", args) == expectedArgs),
           _mainLog.Object,
           It.IsAny<TimeSpan>(),
           It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task UninstallFromDeviceTest()
    {
        var device = Mock.Of<IHardwareDevice>(x => x.Name == DeviceName && x.UDID == UDID);

        // Act
        var result = await _appUninstaller.UninstallDeviceApp(device, AppBundleId);

        // Verify
        Assert.Equal(0, result.ExitCode);

        var expectedArgs = $"--uninstalldevbundleid {StringUtils.FormatArguments(AppBundleId)} --devname {UDID}";

        _processManager.Verify(x => x.ExecuteCommandAsync(
           It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
           _mainLog.Object,
           It.IsAny<TimeSpan>(),
           null,
           It.IsAny<int>(),
           It.IsAny<CancellationToken>()));
    }
}
