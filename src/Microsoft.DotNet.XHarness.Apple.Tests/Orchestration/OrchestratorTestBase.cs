// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Threading;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests.Orchestration;

public abstract class OrchestratorTestBase
{
    protected const string UDID = "8A450AA31EA94191AD6B02455F377CC1";
    protected const string SimulatorName = "iPhone X (13.5) - created by xharness";
    protected const string DeviceName = "iPhone X (14.4)";
    protected const string AppName = "System.Buffers.Tests";
    protected const string AppPath = "/tmp/apps/System.Buffers.Tests.app";
    protected const string BundleIdentifier = "net.dot.System.Buffers.Tests";
    protected const string BundleExecutable = "System.Buffers.Tests";

    protected readonly Mock<IDeviceFinder> _deviceFinder;
    protected readonly Mock<ISimulatorDevice> _simulator;
    protected readonly Mock<IHardwareDevice> _device;
    protected readonly Mock<IAppBundleInformationParser> _appBundleInformationParser;
    protected readonly Mock<IErrorKnowledgeBase> _errorKnowledgeBase;
    protected readonly Mock<IFileBackedLog> _mainLog;
    protected readonly Mock<ILogger> _logger;
    protected readonly Mock<IHelpers> _helpers;
    protected readonly Mock<IAppInstaller> _appInstaller;
    protected readonly Mock<IAppUninstaller> _appUninstaller;
    protected readonly AppBundleInformation _appBundleInformation;
    protected readonly IDiagnosticsData _diagnosticsData;

    protected readonly MockLogs _logs;

    public OrchestratorTestBase()
    {
        _logger = new();
        _mainLog = new();
        _helpers = new();
        _errorKnowledgeBase = new();
        _appInstaller = new();
        _appUninstaller = new();
        _logs = new();

        _logs.AddFile("system.log", LogType.SystemLog.ToString());

        _simulator = new();
        _simulator.Setup(x => x.UDID).Returns(UDID);
        _simulator.Setup(x => x.Name).Returns(SimulatorName);
        _simulator.Setup(x => x.OSVersion).Returns("13.5");
        _simulator.Setup(x => x.Boot(It.IsAny<ILog>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _simulator.Setup(x => x.GetAppBundlePath(It.IsAny<ILog>(), BundleIdentifier, It.IsAny<CancellationToken>())).ReturnsAsync(AppPath);

        _device = new();
        _device.Setup(x => x.UDID).Returns(UDID);
        _device.Setup(x => x.Name).Returns(DeviceName);
        _device.Setup(x => x.OSVersion).Returns("14.2");

        _appBundleInformation = new AppBundleInformation(
            appName: AppName,
            bundleIdentifier: BundleIdentifier,
            appPath: AppPath,
            launchAppPath: AppPath,
            supports32b: false,
            extension: null,
            bundleExecutable: BundleExecutable);

        _appBundleInformationParser = new Mock<IAppBundleInformationParser>();
        _appBundleInformationParser
            .Setup(x => x.ParseFromAppBundle(
                Path.GetFullPath(AppPath),
                It.IsAny<TestTarget>(),
                _mainLog.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_appBundleInformation);

        _diagnosticsData = new CommandDiagnostics(Mock.Of<Extensions.Logging.ILogger>(), TargetPlatform.Apple, "install");

        _deviceFinder = new Mock<IDeviceFinder>();
        _deviceFinder
            .Setup(x => x.FindDevice(
                It.Is<TestTargetOs>(t => t.Platform.IsSimulator()),
                It.IsAny<string?>(),
                It.IsAny<ILog>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DevicePair(_simulator.Object, null));

        _deviceFinder
            .Setup(x => x.FindDevice(
                It.Is<TestTargetOs>(t => !t.Platform.IsSimulator()),
                It.IsAny<string?>(),
                It.IsAny<ILog>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DevicePair(_device.Object, null));
    }

    protected void VerifySimulatorReset(bool shouldBeReset)
    {
        _simulator.Verify(
            x => x.PrepareSimulator(It.IsAny<ILog>(), It.IsAny<string[]>()),
            shouldBeReset ? Times.Once : Times.Never);
    }

    protected void VerifySimulatorCleanUp(bool shouldBeCleanedUp)
    {
        _simulator.Verify(
            x => x.KillEverything(It.IsAny<ILog>()),
            shouldBeCleanedUp ? Times.Once : Times.Never);
    }

    protected void VerifyDiagnosticData(TestTargetOs target)
    {
        Assert.Equal(target.Platform.IsSimulator() ? _simulator.Object.Name : _device.Object.Name, _diagnosticsData.Device);
        Assert.Contains(target.OSVersion!, _diagnosticsData.TargetOS);
    }
}
