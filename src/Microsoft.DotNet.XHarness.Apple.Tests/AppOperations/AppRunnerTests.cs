// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests.AppOperations;

public class AppRunnerTests : AppRunTestBase
{
    [Fact]
    public async Task RunOnSimulatorTest()
    {
        var captureLog = new Mock<ICaptureLog>();
        captureLog.SetupGet(x => x.FullPath).Returns(_simulatorLogPath);
        captureLog.SetupGet(x => x.Description).Returns(LogType.SystemLog.ToString());

        var captureLogFactory = new Mock<ICaptureLogFactory>();
        captureLogFactory
            .Setup(x => x.Create(
               Path.Combine(_logs.Object.Directory, _mockSimulator.Name + ".log"),
               _mockSimulator.SystemLog,
               false,
               LogType.SystemLog))
            .Returns(captureLog.Object);

        SetupLogList(new[] { captureLog.Object });

        // Act
        var appRunner = new AppRunner(
            _processManager.Object,
            _snapshotReporterFactory,
            captureLogFactory.Object,
            Mock.Of<IDeviceLogCapturerFactory>(),
            _mainLog.Object,
            _logs.Object,
            _helpers.Object);

        var result = await appRunner.RunApp(
            _appBundleInfo,
            new TestTargetOs(TestTarget.Simulator_tvOS, null),
            _mockSimulator,
            null,
            timeout: TimeSpan.FromSeconds(30),
            signalAppEnd: false,
            waitForExit: true,
            extraAppArguments: new[] { "--foo=bar", "--xyz" },
            extraEnvVariables: new[] { ("appArg1", "value1") });

        // Verify
        Assert.True(result.Succeeded);

        var expectedArgs = GetExpectedSimulatorMlaunchArgs();

        _processManager
            .Verify(
                x => x.ExecuteCommandAsync(
                   It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                   It.IsAny<ILog>(),
                   It.IsAny<TimeSpan>(),
                   It.IsAny<Dictionary<string, string>>(),
                   It.IsAny<int>(),
                   It.IsAny<CancellationToken>()),
                Times.Once);

        _processManager
            .Verify(
                x => x.ExecuteXcodeCommandAsync(
                   "simctl",
                   It.Is<IList<string>>(args => args.Contains("log") && args.Contains(_mockSimulator.UDID) && args.Contains("stream") && args.Any(a => a.Contains(BundleExecutable))),
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<TimeSpan>(),
                   It.IsAny<CancellationToken>()),
                Times.Once);

        captureLog.Verify(x => x.StartCapture(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunOnDeviceTest()
    {
        var deviceSystemLog = new Mock<IFileBackedLog>();
        deviceSystemLog.SetupGet(x => x.FullPath).Returns(AppBundleIdentifier + "system.log");
        deviceSystemLog.SetupGet(x => x.Description).Returns(LogType.SystemLog.ToString());

        SetupLogList(new[] { deviceSystemLog.Object });

        _logs
            .Setup(x => x.Create("device-" + DeviceName + "-mocked_timestamp.log", LogType.SystemLog.ToString(), It.IsAny<bool?>()))
            .Returns(deviceSystemLog.Object);

        var deviceLogCapturer = new Mock<IDeviceLogCapturer>();

        var deviceLogCapturerFactory = new Mock<IDeviceLogCapturerFactory>();
        deviceLogCapturerFactory
            .Setup(x => x.Create(_mainLog.Object, deviceSystemLog.Object, DeviceName))
            .Returns(deviceLogCapturer.Object);

        var x = _logs.Object.First();

        // Act
        var appRunner = new AppRunner(
            _processManager.Object,
            _snapshotReporterFactory,
            Mock.Of<ICaptureLogFactory>(),
            deviceLogCapturerFactory.Object,
            _mainLog.Object,
            _logs.Object,
            _helpers.Object);

        var result = await appRunner.RunApp(
            _appBundleInfo,
            new TestTargetOs(TestTarget.Device_iOS, null),
            s_mockDevice,
            null,
            timeout: TimeSpan.FromSeconds(30),
            signalAppEnd: false,
            waitForExit: true,
            extraAppArguments: new[] { "--foo=bar", "--xyz" },
            extraEnvVariables: new[] { ("appArg1", "value1") });

        // Verify
        Assert.True(result.Succeeded);

        var expectedArgs = GetExpectedDeviceMlaunchArgs();

        _processManager
            .Verify(
                x => x.ExecuteCommandAsync(
                   It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<TimeSpan>(),
                   It.IsAny<Dictionary<string, string>>(),
                   It.IsAny<int>(),
                   It.IsAny<CancellationToken>()),
                Times.Once);

        _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);

        deviceSystemLog.Verify(x => x.Dispose(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunOnDeviceWithAppEndSignalTest()
    {
        var deviceSystemLog = new Mock<IFileBackedLog>();
        deviceSystemLog.SetupGet(x => x.FullPath).Returns(AppBundleIdentifier + "system.log");
        deviceSystemLog.SetupGet(x => x.Description).Returns(LogType.SystemLog.ToString());

        SetupLogList(new[] { deviceSystemLog.Object });

        _logs
            .Setup(x => x.Create("device-" + DeviceName + "-mocked_timestamp.log", LogType.SystemLog.ToString(), It.IsAny<bool?>()))
            .Returns(deviceSystemLog.Object);

        var deviceLogCapturer = new Mock<IDeviceLogCapturer>();

        var deviceLogCapturerFactory = new Mock<IDeviceLogCapturerFactory>();
        deviceLogCapturerFactory
            .Setup(x => x.Create(_mainLog.Object, deviceSystemLog.Object, DeviceName))
            .Returns(deviceLogCapturer.Object);

        var testEndSignal = Guid.NewGuid();
        _helpers
            .Setup(x => x.GenerateGuid())
            .Returns(testEndSignal);

        List<MlaunchArguments> mlaunchArguments = new();
        List<IFileBackedLog> appOutputLogs = new();
        List<CancellationToken> cancellationTokens = new();

        // Endlessly running mlaunch until it gets cancelled by the signal
        var mlaunchCompleted = new TaskCompletionSource<ProcessExecutionResult>();
        var appStarted = new TaskCompletionSource();

        _processManager
            .Setup(x => x.ExecuteCommandAsync(
                   Capture.In(mlaunchArguments),
                   It.IsAny<ILog>(),
                   Capture.In(appOutputLogs),
                   Capture.In(appOutputLogs),
                   It.IsAny<TimeSpan>(),
                   It.IsAny<Dictionary<string, string>?>(),
                   It.IsAny<int>(),
                   Capture.In(cancellationTokens)))
            .Callback(() =>
            {
                // Signal we have started mlaunch
                appStarted.SetResult();

                // When mlaunch gets signalled to shut down, shut down even our fake mlaunch
                cancellationTokens.Last().Register(() => mlaunchCompleted.SetResult(new ProcessExecutionResult
                {
                    TimedOut = true,
                }));
            })
            .Returns(mlaunchCompleted.Task);

        // Act
        var appRunner = new AppRunner(
            _processManager.Object,
            _snapshotReporterFactory,
            Mock.Of<ICaptureLogFactory>(),
            deviceLogCapturerFactory.Object,
            _mainLog.Object,
            _logs.Object,
            _helpers.Object);

        var runTask = appRunner.RunApp(
            _appBundleInfo,
            new TestTargetOs(TestTarget.Device_iOS, null),
            s_mockDevice,
            null,
            timeout: TimeSpan.FromSeconds(30),
            signalAppEnd: true,
            waitForExit: true,
            Array.Empty<string>(),
            Array.Empty<(string, string)>());

        // Everything should hang now since we mimicked mlaunch not being able to tell the app quits
        // We will wait for XHarness to kick off the mlaunch (the app)
        Assert.False(runTask.IsCompleted);
        await Task.WhenAny(appStarted.Task, Task.Delay(1000));

        // XHarness should still be running
        Assert.False(runTask.IsCompleted);

        // mlaunch should be started
        Assert.True(appStarted.Task.IsCompleted);

        // We will mimick the app writing the end signal
        var appLog = appOutputLogs.First();
        appLog.WriteLine(testEndSignal.ToString());

        // AppTester should now complete fine
        var result = await runTask;

        // Verify
        Assert.True(result.Succeeded);

        var expectedArgs = $"-setenv=RUN_END_TAG={testEndSignal} " +
            "--disable-memory-limits " +
            $"--devname {s_mockDevice.DeviceIdentifier} " +
            $"--launchdevbundleid {AppBundleIdentifier} " +
            "--wait-for-exit";

        Assert.Equal(mlaunchArguments.Last().AsCommandLine(), expectedArgs);

        _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);

        deviceSystemLog.Verify(x => x.Dispose(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunOnMacCatalystTest()
    {
        var captureLog = new Mock<ICaptureLog>();
        captureLog.SetupGet(x => x.FullPath).Returns(_simulatorLogPath);
        captureLog.SetupGet(x => x.Description).Returns(LogType.SystemLog.ToString());

        var captureLogFactory = new Mock<ICaptureLogFactory>();
        captureLogFactory
            .Setup(x => x.Create(
               It.IsAny<string>(),
               It.IsAny<string>(),
               false,
               LogType.SystemLog))
            .Returns(captureLog.Object);

        SetupLogList(new[] { captureLog.Object });

        // Act
        var appRunner = new AppRunner(
            _processManager.Object,
            _snapshotReporterFactory,
            captureLogFactory.Object,
            Mock.Of<IDeviceLogCapturerFactory>(),
            _mainLog.Object,
            _logs.Object,
            _helpers.Object);

        var result = await appRunner.RunMacCatalystApp(
            _appBundleInfo,
            timeout: TimeSpan.FromSeconds(30),
            signalAppEnd: false,
            waitForExit: true,
            extraAppArguments: new[] { "--foo=bar", "--xyz" },
            extraEnvVariables: new[] { ("appArg1", "value1") });

        // Verify
        Assert.True(result.Succeeded);

        var expectedArgs = GetExpectedSimulatorMlaunchArgs();

        _processManager
            .Verify(
                x => x.ExecuteCommandAsync(
                   "open",
                   It.Is<IList<string>>(args => args[0] == "-n" && args[1] == "-W" && args[2] == s_appPath),
                   _mainLog.Object,
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<TimeSpan>(),
                   It.IsAny<Dictionary<string, string>>(),
                   It.IsAny<CancellationToken>()),
                Times.Once);

        _processManager
            .Verify(
                x => x.ExecuteCommandAsync(
                   "log",
                   It.Is<IList<string>>(args => args.Contains("stream") && args.Any(a => a.Contains(BundleExecutable))),
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<TimeSpan>(),
                   It.IsAny<Dictionary<string, string>?>(),
                   It.IsAny<CancellationToken>()),
                Times.Once);

        captureLog.Verify(x => x.StartCapture(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunOnDeviceNoWaitTest()
    {
        var deviceSystemLog = new Mock<IFileBackedLog>();
        deviceSystemLog.SetupGet(x => x.FullPath).Returns(AppBundleIdentifier + "system.log");
        deviceSystemLog.SetupGet(x => x.Description).Returns(LogType.SystemLog.ToString());

        SetupLogList(new[] { deviceSystemLog.Object });

        _logs
            .Setup(x => x.Create("device-" + DeviceName + "-mocked_timestamp.log", LogType.SystemLog.ToString(), It.IsAny<bool?>()))
            .Returns(deviceSystemLog.Object);

        var deviceLogCapturer = new Mock<IDeviceLogCapturer>();

        var deviceLogCapturerFactory = new Mock<IDeviceLogCapturerFactory>();
        deviceLogCapturerFactory
            .Setup(x => x.Create(_mainLog.Object, deviceSystemLog.Object, DeviceName))
            .Returns(deviceLogCapturer.Object);

        var x = _logs.Object.First();

        // Act
        var appRunner = new AppRunner(
            _processManager.Object,
            _snapshotReporterFactory,
            Mock.Of<ICaptureLogFactory>(),
            deviceLogCapturerFactory.Object,
            _mainLog.Object,
            _logs.Object,
            _helpers.Object);

        var result = await appRunner.RunApp(
            _appBundleInfo,
            new TestTargetOs(TestTarget.Device_iOS, null),
            s_mockDevice,
            null,
            timeout: TimeSpan.FromSeconds(30),
            signalAppEnd: false,
            waitForExit: false,
            extraAppArguments: new[] { "--foo=bar", "--xyz" },
            extraEnvVariables: new[] { ("appArg1", "value1") });

        // Verify
        Assert.True(result.Succeeded);

        var expectedArgs = GetExpectedDeviceMlaunchArgs();

        _processManager
            .Verify(
                x => x.ExecuteCommandAsync(
                   It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs.Replace(" --wait-for-exit", null)),
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<TimeSpan>(),
                   It.IsAny<Dictionary<string, string>>(),
                   It.IsAny<int>(),
                   It.IsAny<CancellationToken>()),
                Times.Once);

        _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);

        deviceSystemLog.Verify(x => x.Dispose(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunOnSimulatorNoWaitTest()
    {
        var captureLog = new Mock<ICaptureLog>();
        captureLog.SetupGet(x => x.FullPath).Returns(_simulatorLogPath);
        captureLog.SetupGet(x => x.Description).Returns(LogType.SystemLog.ToString());

        var captureLogFactory = new Mock<ICaptureLogFactory>();
        captureLogFactory
            .Setup(x => x.Create(
               Path.Combine(_logs.Object.Directory, _mockSimulator.Name + ".log"),
               _mockSimulator.SystemLog,
               false,
               LogType.SystemLog))
            .Returns(captureLog.Object);

        SetupLogList(new[] { captureLog.Object });

        var expectedArgs = GetExpectedSimulatorMlaunchArgs();

        var appLaunchedTask = new TaskCompletionSource();
        ILog? appLog = null;

        _processManager
            .Setup(
                x => x.ExecuteCommandAsync(
                   It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                   It.IsAny<ILog>(),
                   It.IsAny<TimeSpan>(),
                   It.IsAny<Dictionary<string, string>>(),
                   It.IsAny<int>(),
                   It.IsAny<CancellationToken>()))
            .Callback((MlaunchArguments args, ILog log, TimeSpan timeout, Dictionary<string, string> env, int verbosity, CancellationToken? ct) =>
            {
                appLog = log;
                appLaunchedTask.SetResult();
            })
            .Returns(new TaskCompletionSource<ProcessExecutionResult>().Task); // This task must never complete (it represents running app)

        // Act
        var appRunner = new AppRunner(
            _processManager.Object,
            _snapshotReporterFactory,
            captureLogFactory.Object,
            Mock.Of<IDeviceLogCapturerFactory>(),
            _mainLog.Object,
            _logs.Object,
            _helpers.Object);

        var runTask = appRunner.RunApp(
            _appBundleInfo,
            new TestTargetOs(TestTarget.Simulator_tvOS, null),
            _mockSimulator,
            null,
            timeout: TimeSpan.FromSeconds(30),
            signalAppEnd: false,
            waitForExit: false,
            extraAppArguments: new[] { "--foo=bar", "--xyz" },
            extraEnvVariables: new[] { ("appArg1", "value1") });

        // No we wait for the launch of the app (which will then hang and the ScanLog will start waiting for the launch signal)
        await appLaunchedTask.Task;

        Assert.False(runTask.IsCompleted);

        // Now we send the signal that the app has launched
        Assert.NotNull(appLog);
        appLog!.WriteLine($"Some message");
        appLog!.WriteLine($"Xamarin.Hosting: Launched {AppBundleIdentifier} with pid 39402");
        appLog!.WriteLine($"Some other message");

        // We should now be able to return from here since the ScanLog will finish
        var result = await runTask;

        // Verify
        Assert.True(result.Succeeded);

        _processManager
            .Verify(
                x => x.ExecuteCommandAsync(
                   It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                   It.IsAny<ILog>(),
                   It.IsAny<TimeSpan>(),
                   It.IsAny<Dictionary<string, string>>(),
                   It.IsAny<int>(),
                   It.IsAny<CancellationToken>()),
                Times.Once);

        _processManager
            .Verify(
                x => x.ExecuteXcodeCommandAsync(
                   "simctl",
                   It.Is<IList<string>>(args => args.Contains("log") && args.Contains(_mockSimulator.UDID) && args.Contains("stream") && args.Any(a => a.Contains(BundleExecutable))),
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<TimeSpan>(),
                   It.IsAny<CancellationToken>()),
                Times.Once);

        captureLog.Verify(x => x.StartCapture(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunOnSimulatorNoWaitNoLaunchSignalTest()
    {
        var captureLog = new Mock<ICaptureLog>();
        captureLog.SetupGet(x => x.FullPath).Returns(_simulatorLogPath);
        captureLog.SetupGet(x => x.Description).Returns(LogType.SystemLog.ToString());

        var captureLogFactory = new Mock<ICaptureLogFactory>();
        captureLogFactory
            .Setup(x => x.Create(
               Path.Combine(_logs.Object.Directory, _mockSimulator.Name + ".log"),
               _mockSimulator.SystemLog,
               false,
               LogType.SystemLog))
            .Returns(captureLog.Object);

        SetupLogList(new[] { captureLog.Object });

        var expectedArgs = GetExpectedSimulatorMlaunchArgs();

        var appLaunchedTask = new TaskCompletionSource();
        var appRunTask = new TaskCompletionSource<ProcessExecutionResult>();

        _processManager
            .Setup(
                x => x.ExecuteCommandAsync(
                   It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                   It.IsAny<ILog>(),
                   It.IsAny<TimeSpan>(),
                   It.IsAny<Dictionary<string, string>>(),
                   It.IsAny<int>(),
                   It.IsAny<CancellationToken>()))
            .Callback((MlaunchArguments args, ILog log, TimeSpan timeout, Dictionary<string, string> env, int verbosity, CancellationToken? ct) =>
            {
                appLaunchedTask.SetResult();
            })
            .Returns(appRunTask.Task); // This task will complete and the ScanLog task won't (we won't log the "app launched" message)

        // Act
        var appRunner = new AppRunner(
            _processManager.Object,
            _snapshotReporterFactory,
            captureLogFactory.Object,
            Mock.Of<IDeviceLogCapturerFactory>(),
            _mainLog.Object,
            _logs.Object,
            _helpers.Object);

        var runTask = appRunner.RunApp(
            _appBundleInfo,
            new TestTargetOs(TestTarget.Simulator_tvOS, null),
            _mockSimulator,
            null,
            timeout: TimeSpan.FromSeconds(30),
            signalAppEnd: false,
            waitForExit: false,
            extraAppArguments: new[] { "--foo=bar", "--xyz" },
            extraEnvVariables: new[] { ("appArg1", "value1") });

        // No we wait for the code to start launching the app
        await appLaunchedTask.Task;

        Assert.False(runTask.IsCompleted);

        // In this phase, the code waits for both ScanLog or the app run task
        // We will simulate a case when the app never reports back (never launches)
        appRunTask.SetResult(new ProcessExecutionResult
        {
            ExitCode = 137, // This is what we get when app run times out and is killed by our timeout
            TimedOut = true,
        });

        var result = await runTask;

        // Verify
        Assert.False(result.Succeeded);
        Assert.True(result.TimedOut);

        _processManager
            .Verify(
                x => x.ExecuteCommandAsync(
                   It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                   It.IsAny<ILog>(),
                   It.IsAny<TimeSpan>(),
                   It.IsAny<Dictionary<string, string>>(),
                   It.IsAny<int>(),
                   It.IsAny<CancellationToken>()),
                Times.Once);

        _processManager
            .Verify(
                x => x.ExecuteXcodeCommandAsync(
                   "simctl",
                   It.Is<IList<string>>(args => args.Contains("log") && args.Contains(_mockSimulator.UDID) && args.Contains("stream") && args.Any(a => a.Contains(BundleExecutable))),
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<TimeSpan>(),
                   It.IsAny<CancellationToken>()),
                Times.Once);

        captureLog.Verify(x => x.StartCapture(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RunOnMacCatalystNoWaitTest()
    {
        var captureLog = new Mock<ICaptureLog>();
        captureLog.SetupGet(x => x.FullPath).Returns(_simulatorLogPath);
        captureLog.SetupGet(x => x.Description).Returns(LogType.SystemLog.ToString());

        var captureLogFactory = new Mock<ICaptureLogFactory>();
        captureLogFactory
            .Setup(x => x.Create(
               It.IsAny<string>(),
               It.IsAny<string>(),
               false,
               LogType.SystemLog))
            .Returns(captureLog.Object);

        SetupLogList(new[] { captureLog.Object });

        // Act
        var appRunner = new AppRunner(
            _processManager.Object,
            _snapshotReporterFactory,
            captureLogFactory.Object,
            Mock.Of<IDeviceLogCapturerFactory>(),
            _mainLog.Object,
            _logs.Object,
            _helpers.Object);

        var result = await appRunner.RunMacCatalystApp(
            _appBundleInfo,
            timeout: TimeSpan.FromSeconds(30),
            signalAppEnd: false,
            waitForExit: false,
            extraAppArguments: Array.Empty<string>(),
            extraEnvVariables: Array.Empty<(string, string)>());

        // Verify
        Assert.True(result.Succeeded);

        var expectedArgs = GetExpectedSimulatorMlaunchArgs();

        _processManager
            .Verify(
                x => x.ExecuteCommandAsync(
                   "open",
                   It.Is<IList<string>>(args => args.Count == 2 && args[0] == "-n" && args[1] == s_appPath),
                   _mainLog.Object,
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<TimeSpan>(),
                   It.IsAny<Dictionary<string, string>>(),
                   It.IsAny<CancellationToken>()),
                Times.Once);

        captureLog.Verify(x => x.StartCapture(), Times.AtLeastOnce);
    }

    private static string GetExpectedDeviceMlaunchArgs() =>
        "-argument=--foo=bar " +
        "-argument=--xyz " +
        "-setenv=appArg1=value1 " +
        "--disable-memory-limits " +
        $"--devname {s_mockDevice.DeviceIdentifier} " +
        $"--launchdevbundleid {AppBundleIdentifier} " +
        "--wait-for-exit";

    private string GetExpectedSimulatorMlaunchArgs() =>
        "-argument=--foo=bar " +
        "-argument=--xyz " +
        "-setenv=appArg1=value1 " +
        $"--device=:v2:udid={_mockSimulator.UDID} " +
        $"--launchsimbundleid={AppBundleIdentifier}";

    private void SetupLogList(IEnumerable<IFileBackedLog> logs)
    {
        _logs
            .Setup(x => x.GetEnumerator())
            .Returns(() => logs.GetEnumerator());

        _logs
            .Setup(m => m.Count)
            .Returns(() => logs.Count());

        _logs
            .Setup(m => m[It.IsAny<int>()])
            .Returns<int>(i => logs.ElementAt(i));
    }
}
