// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests.AppOperations;

public class AppInstallerTests : IDisposable
{
    private static readonly string s_appPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private static readonly string s_appIdentifier = Guid.NewGuid().ToString();
    private const string UDID = "8A450AA31EA94191AD6B02455F377CC1";
    private static readonly IDevice s_mockDevice = Mock.Of<IDevice>(x =>
        x.UDID == UDID &&
        x.Name == "Test iPhone" &&
        x.OSVersion == "13.4");
    private readonly Mock<IMlaunchProcessManager> _processManager;
    private readonly Mock<ILog> _mainLog;
    private readonly AppBundleInformation _appBundleInformation;

    public AppInstallerTests()
    {
        _mainLog = new Mock<ILog>();

        _processManager = new Mock<IMlaunchProcessManager>();
        _processManager.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult() { ExitCode = 0 }));

        Directory.CreateDirectory(s_appPath);
        _appBundleInformation = new AppBundleInformation(
            appName: "AppName",
            bundleIdentifier: s_appIdentifier,
            appPath: s_appPath,
            launchAppPath: s_appPath,
            supports32b: false,
            extension: null);
    }

    public void Dispose()
    {
        Directory.Delete(s_appPath, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task InstallOnSimulatorTest()
    {
        // Act
        var appInstaller = new AppInstaller(_processManager.Object, _mainLog.Object);

        var result = await appInstaller.InstallApp(_appBundleInformation, new TestTargetOs(TestTarget.Simulator_iOS64, null), s_mockDevice);

        // Verify
        Assert.Equal(0, result.ExitCode);

        var expectedArgs = $"--device=:v2:udid={s_mockDevice.UDID} --installsim {StringUtils.FormatArguments(s_appPath)}";

        _processManager.Verify(x => x.ExecuteCommandAsync(
           It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
           _mainLog.Object,
           It.IsAny<TimeSpan>(),
           null,
           It.IsAny<int>(),
           It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task InstallOnDeviceTest()
    {
        // Act
        var appInstaller = new AppInstaller(_processManager.Object, _mainLog.Object);

        var result = await appInstaller.InstallApp(_appBundleInformation, new TestTargetOs(TestTarget.Device_iOS, null), s_mockDevice);

        // Verify
        Assert.Equal(0, result.ExitCode);

        var expectedArgs = $"--devname {s_mockDevice.UDID} --installdev {StringUtils.FormatArguments(s_appPath)}";

        _processManager.Verify(x => x.ExecuteCommandAsync(
           It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
           _mainLog.Object,
           It.IsAny<TimeSpan>(),
           null,
           It.IsAny<int>(),
           It.IsAny<CancellationToken>()));
    }
}
