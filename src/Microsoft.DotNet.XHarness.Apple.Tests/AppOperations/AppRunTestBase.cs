// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Moq;

namespace Microsoft.DotNet.XHarness.Apple.Tests.AppOperations;

public abstract class AppRunTestBase : IDisposable
{
    protected const string AppName = "System.Text.Json.Tests.app";
    protected const string AppBundleIdentifier = "net.dot.System.Text.Json.Tests";
    protected const string BundleExecutable = "System.Text.Json.Tests";
    protected const string SimulatorDeviceName = "Test iPhone simulator";
    protected const string DeviceName = "Test iPhone";

    protected static readonly string s_outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    protected static readonly string s_appPath = Path.Combine(s_outputPath, AppName);

    protected static readonly IHardwareDevice s_mockDevice = Mock.Of<IHardwareDevice>(x =>
        x.BuildVersion == "17A577" &&
        x.DeviceClass == DeviceClass.iPhone &&
        x.DeviceIdentifier == "8A450AA31EA94191AD6B02455F377CC1" &&
        x.UDID == "8A450AA31EA94191AD6B02455F377CC1" &&
        x.InterfaceType == "Usb" &&
        x.IsUsableForDebugging == true &&
        x.Name == DeviceName &&
        x.ProductType == "iPhone12,1" &&
        x.ProductVersion == "13.0");

    protected readonly AppBundleInformation _appBundleInfo =
        new(appName: AppName,
            bundleIdentifier: AppBundleIdentifier,
            appPath: s_appPath,
            launchAppPath: s_appPath,
            supports32b: false,
            extension: null,
            bundleExecutable: BundleExecutable);

    protected readonly string _simulatorLogPath = Path.Combine(Path.GetTempPath(), "simulator-logs");

    protected readonly ISimulatorDevice _mockSimulator;

    protected readonly Mock<IMlaunchProcessManager> _processManager;
    protected readonly Mock<ILogs> _logs;
    protected readonly Mock<IFileBackedLog> _mainLog;
    protected readonly Mock<ICrashSnapshotReporter> _snapshotReporter;
    protected readonly Mock<IHelpers> _helpers;

    protected readonly ICrashSnapshotReporterFactory _snapshotReporterFactory;
    protected readonly IFileBackedLog _appLog;

    protected AppRunTestBase()
    {
        _mainLog = new Mock<IFileBackedLog>();

        _processManager = new Mock<IMlaunchProcessManager>();
        _processManager.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult() { ExitCode = 0 }));

        _snapshotReporter = new Mock<ICrashSnapshotReporter>();

        _appLog = Mock.Of<IFileBackedLog>(
            x => x.FullPath == $"./{BundleExecutable}.log" && x.Description == LogType.ApplicationLog.ToString());

        _logs = new Mock<ILogs>();
        _logs
            .SetupGet(x => x.Directory)
            .Returns(Path.Combine(s_outputPath, "logs"));
        _logs
            .Setup(x => x.CreateFile($"{AppBundleIdentifier}-mocked_timestamp.log", It.IsAny<LogType>()))
            .Returns($"./{AppBundleIdentifier}-mocked_timestamp.log");
        _logs
            .Setup(x => x.Create(BundleExecutable + ".log", It.IsAny<string>(), It.IsAny<bool?>()))
            .Returns(_appLog);
        _logs
            .Setup(x => x.Create(AppBundleIdentifier + ".log", It.IsAny<string>(), It.IsAny<bool?>()))
            .Returns(_appLog);

        var factory2 = new Mock<ICrashSnapshotReporterFactory>();
        factory2.SetReturnsDefault(_snapshotReporter.Object);
        _snapshotReporterFactory = factory2.Object;

        _mockSimulator = Mock.Of<ISimulatorDevice>(x =>
            x.UDID == "58F21118E4D34FD69EAB7860BB9B38A0" &&
            x.Name == SimulatorDeviceName &&
            x.LogPath == _simulatorLogPath &&
            x.SystemLog == Path.Combine(_simulatorLogPath, "system.log"));

        _helpers = new Mock<IHelpers>();
        _helpers
            .Setup(x => x.GetTerminalName(It.IsAny<int>()))
            .Returns("tty1");
        _helpers
            .Setup(x => x.GenerateStableGuid(It.IsAny<string>()))
            .Returns(Guid.NewGuid());
        _helpers
            .SetupGet(x => x.Timestamp)
            .Returns("mocked_timestamp");
        _helpers
            .Setup(x => x.GetLocalIpAddresses())
            .Returns(new[] { IPAddress.Loopback, IPAddress.IPv6Loopback });

        Directory.CreateDirectory(s_outputPath);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(s_outputPath, true);
        }
        catch
        {
            // Concurrency can cause these
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }
}
