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
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests.AppOperations;

public class AppTesterTests : AppRunTestBase
{
    private const int Port = 1020;

    private readonly Mock<ISimpleListener> _listener;
    private readonly Mock<ITestReporter> _testReporter;
    private readonly Mock<ITunnelBore> _tunnelBore;
    private readonly Mock<ISimpleListenerFactory> _listenerFactory;

    private readonly ITestReporterFactory _testReporterFactory;

    public AppTesterTests()
    {
        _listener = new Mock<ISimpleListener>();
        _listener
            .SetupGet(x => x.ConnectedTask)
            .Returns(Task.FromResult(true));

        _testReporter = new Mock<ITestReporter>();
        _testReporter
            .Setup(r => r.Success)
            .Returns(true);
        _testReporter
            .Setup(r => r.ParseResult())
            .ReturnsAsync((TestExecutingResult.Succeeded, "Tests run: 1194 Passed: 1191 Inconclusive: 0 Failed: 0 Ignored: 0"));
        _testReporter
            .Setup(x => x.CollectSimulatorResult(It.IsAny<ProcessExecutionResult>()))
            .Returns(Task.CompletedTask);

        _tunnelBore = new Mock<ITunnelBore>();
        _tunnelBore.Setup(t => t.Close(It.IsAny<string>()));

        _listenerFactory = new Mock<ISimpleListenerFactory>();
        _listenerFactory.SetReturnsDefault((ListenerTransport.Tcp, _listener.Object, "listener-temp-file"));
        _listenerFactory.Setup(f => f.TunnelBore).Returns(_tunnelBore.Object);
        _listener.Setup(x => x.InitializeAndGetPort()).Returns(Port);

        var factory2 = new Mock<ICrashSnapshotReporterFactory>();
        factory2.SetReturnsDefault(_snapshotReporter.Object);

        var factory3 = new Mock<ITestReporterFactory>();
        factory3.SetReturnsDefault(_testReporter.Object);
        _testReporterFactory = factory3.Object;

        Directory.CreateDirectory(s_outputPath);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TestOnSimulatorTest(bool useTunnel)
    {
        var testResultFilePath = Path.GetTempFileName();
        var listenerLogFile = Mock.Of<IFileBackedLog>(x => x.FullPath == testResultFilePath);
        File.WriteAllLines(testResultFilePath, new[] { "Some result here", "Tests run: 124", "Some result there" });

        _logs
            .Setup(x => x.Create("test-ios-simulator-64-mocked_timestamp.log", "TestLog", It.IsAny<bool?>()))
            .Returns(listenerLogFile);

        var captureLog = new Mock<ICaptureLog>();
        captureLog.SetupGet(x => x.FullPath).Returns(_simulatorLogPath);

        var captureLogFactory = new Mock<ICaptureLogFactory>();
        captureLogFactory
            .Setup(x => x.Create(
               Path.Combine(_logs.Object.Directory, _mockSimulator.Name + ".log"),
               _mockSimulator.SystemLog,
               false,
               It.IsAny<LogType>()))
            .Returns(captureLog.Object);

        _listenerFactory.Setup(f => f.UseTunnel).Returns(useTunnel);

        // Act
        var appTester = new AppTester(
            _processManager.Object,
            _listenerFactory.Object,
            _snapshotReporterFactory,
            captureLogFactory.Object,
            Mock.Of<IDeviceLogCapturerFactory>(),
            _testReporterFactory,
            new XmlResultParser(),
            _mainLog.Object,
            _logs.Object,
            _helpers.Object);

        var (result, resultMessage) = await appTester.TestApp(
            _appBundleInfo,
            new TestTargetOs(TestTarget.Simulator_tvOS, null),
            _mockSimulator,
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30),
            signalAppEnd: false,
            extraAppArguments: new string[] { "--foo=bar", "--xyz" },
            extraEnvVariables: new[] { ("appArg1", "value1") });

        // Verify
        Assert.Equal(TestExecutingResult.Succeeded, result);
        Assert.Equal("Tests run: 1194 Passed: 1191 Inconclusive: 0 Failed: 0 Ignored: 0", resultMessage);

        var expectedArgs = GetExpectedSimulatorMlaunchArgs();

        _processManager
            .Verify(
                x => x.ExecuteCommandAsync(
                   It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                   _mainLog.Object,
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

        _listener.Verify(x => x.InitializeAndGetPort(), Times.AtLeastOnce);
        _listener.Verify(x => x.StartAsync(), Times.AtLeastOnce);
        _listener.Verify(x => x.Dispose(), Times.AtLeastOnce);

        captureLog.Verify(x => x.StartCapture(), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TestOnDeviceTest(bool useTunnel)
    {
        var deviceSystemLog = new Mock<IFileBackedLog>();
        deviceSystemLog.SetupGet(x => x.FullPath).Returns(Path.GetTempFileName());

        var deviceLogCapturer = new Mock<IDeviceLogCapturer>();

        var deviceLogCapturerFactory = new Mock<IDeviceLogCapturerFactory>();
        deviceLogCapturerFactory
            .Setup(x => x.Create(_mainLog.Object, deviceSystemLog.Object, DeviceName))
            .Returns(deviceLogCapturer.Object);

        var testResultFilePath = Path.GetTempFileName();
        var listenerLogFile = Mock.Of<IFileBackedLog>(x => x.FullPath == testResultFilePath);
        File.WriteAllLines(testResultFilePath, new[] { "Some result here", "Tests run: 124", "Some result there" });

        _logs
            .Setup(x => x.Create("test-ios-device-mocked_timestamp.log", "TestLog", It.IsAny<bool?>()))
            .Returns(listenerLogFile);

        _logs
            .Setup(x => x.Create($"device-{DeviceName}-mocked_timestamp.log", LogType.SystemLog.ToString(), It.IsAny<bool?>()))
            .Returns(deviceSystemLog.Object);

        // set tunnel bore expectation
        if (useTunnel)
        {
            _tunnelBore.Setup(t => t.Create(DeviceName, It.IsAny<ILog>()));
        }

        _listenerFactory
            .Setup(f => f.UseTunnel)
            .Returns(useTunnel);

        // Act
        var appTester = new AppTester(
            _processManager.Object,
            _listenerFactory.Object,
            _snapshotReporterFactory,
            Mock.Of<ICaptureLogFactory>(),
            deviceLogCapturerFactory.Object,
            _testReporterFactory,
            new XmlResultParser(),
            _mainLog.Object,
            _logs.Object,
            _helpers.Object);

        var (result, resultMessage) = await appTester.TestApp(
            _appBundleInfo,
            new TestTargetOs(TestTarget.Device_iOS, null),
            s_mockDevice,
            null,
            timeout: TimeSpan.FromSeconds(30),
            testLaunchTimeout: TimeSpan.FromSeconds(30),
            signalAppEnd: false,
            extraAppArguments: new[] { "--foo=bar", "--xyz" },
            extraEnvVariables: new[] { ("appArg1", "value1") });

        // Verify
        Assert.Equal(TestExecutingResult.Succeeded, result);
        Assert.Equal("Tests run: 1194 Passed: 1191 Inconclusive: 0 Failed: 0 Ignored: 0", resultMessage);

        var expectedArgs = GetExpectedDeviceMlaunchArgs(
            useTunnel: useTunnel,
            extraArgs: "-setenv=appArg1=value1 -argument=--foo=bar -argument=--xyz ");

        _processManager
            .Verify(
                x => x.ExecuteCommandAsync(
                   It.Is<MlaunchArguments>(args => args.AsCommandLine() == expectedArgs),
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<TimeSpan>(),
                   It.Is<Dictionary<string, string>>(d => d["appArg1"] == "value1"),
                   It.IsAny<int>(),
                   It.IsAny<CancellationToken>()),
                Times.Once);

        _listener.Verify(x => x.InitializeAndGetPort(), Times.AtLeastOnce);
        _listener.Verify(x => x.StartAsync(), Times.AtLeastOnce);
        _listener.Verify(x => x.Dispose(), Times.AtLeastOnce);

        // verify that we do close the tunnel when it was used
        // we dont want to leak a process
        if (useTunnel)
        {
            _tunnelBore.Verify(t => t.Close(s_mockDevice.DeviceIdentifier));
        }

        _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);

        deviceSystemLog.Verify(x => x.Dispose(), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("MyClass.MyMethod")]
    [InlineData("MyClass.MyMethod", "MyClass.MySecondMethod")]
    public async Task TestOnDeviceWithSkippedTestsTest(params string[] skippedTests)
    {
        var deviceSystemLog = new Mock<IFileBackedLog>();
        deviceSystemLog.SetupGet(x => x.FullPath).Returns(Path.GetTempFileName());

        var deviceLogCapturer = new Mock<IDeviceLogCapturer>();

        var deviceLogCapturerFactory = new Mock<IDeviceLogCapturerFactory>();
        deviceLogCapturerFactory
            .Setup(x => x.Create(_mainLog.Object, deviceSystemLog.Object, DeviceName))
            .Returns(deviceLogCapturer.Object);

        var testResultFilePath = Path.GetTempFileName();
        var listenerLogFile = Mock.Of<IFileBackedLog>(x => x.FullPath == testResultFilePath);
        File.WriteAllLines(testResultFilePath, new[] { "Some result here", "Tests run: 124", "Some result there" });

        _logs
            .Setup(x => x.Create("test-ios-device-mocked_timestamp.log", "TestLog", It.IsAny<bool?>()))
            .Returns(listenerLogFile);

        _logs
            .Setup(x => x.Create($"device-{DeviceName}-mocked_timestamp.log", LogType.SystemLog.ToString(), It.IsAny<bool?>()))
            .Returns(deviceSystemLog.Object);

        // Act
        var appTester = new AppTester(
            _processManager.Object,
            _listenerFactory.Object,
            _snapshotReporterFactory,
            Mock.Of<ICaptureLogFactory>(),
            deviceLogCapturerFactory.Object,
            _testReporterFactory,
            new XmlResultParser(),
            _mainLog.Object,
            _logs.Object,
            _helpers.Object);

        var (result, resultMessage) = await appTester.TestApp(
            _appBundleInfo,
            new TestTargetOs(TestTarget.Device_iOS, null),
            s_mockDevice,
            null,
            timeout: TimeSpan.FromSeconds(30),
            testLaunchTimeout: TimeSpan.FromSeconds(30),
            signalAppEnd: false,
            extraAppArguments: new[] { "--foo=bar", "--xyz" },
            extraEnvVariables: new[] { ("appArg1", "value1") },
            skippedMethods: skippedTests);

        // Verify
        Assert.Equal(TestExecutingResult.Succeeded, result);
        Assert.Equal("Tests run: 1194 Passed: 1191 Inconclusive: 0 Failed: 0 Ignored: 0", resultMessage);

        var skippedTestsArg = $"-setenv=NUNIT_RUN_ALL=false -setenv=NUNIT_SKIPPED_METHODS={string.Join(',', skippedTests)} ";

        var expectedArgs = GetExpectedDeviceMlaunchArgs(skippedTestsArg, extraArgs: "-setenv=appArg1=value1 -argument=--foo=bar -argument=--xyz ");

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

        _listener.Verify(x => x.InitializeAndGetPort(), Times.AtLeastOnce);
        _listener.Verify(x => x.StartAsync(), Times.AtLeastOnce);
        _listener.Verify(x => x.Dispose(), Times.AtLeastOnce);

        _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);

        deviceSystemLog.Verify(x => x.Dispose(), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("MyClass")]
    [InlineData("MyClass", "MySecondClass")]
    public async Task TestOnDeviceWithSkippedClassesTestTest(params string[] skippedClasses)
    {
        var deviceSystemLog = new Mock<IFileBackedLog>();
        deviceSystemLog.SetupGet(x => x.FullPath).Returns(Path.GetTempFileName());

        var deviceLogCapturer = new Mock<IDeviceLogCapturer>();

        var deviceLogCapturerFactory = new Mock<IDeviceLogCapturerFactory>();
        deviceLogCapturerFactory
            .Setup(x => x.Create(_mainLog.Object, deviceSystemLog.Object, DeviceName))
            .Returns(deviceLogCapturer.Object);

        var testResultFilePath = Path.GetTempFileName();
        var listenerLogFile = Mock.Of<IFileBackedLog>(x => x.FullPath == testResultFilePath);
        File.WriteAllLines(testResultFilePath, new[] { "Some result here", "Tests run: 124", "Some result there" });

        _logs
            .Setup(x => x.Create("test-ios-device-mocked_timestamp.log", "TestLog", It.IsAny<bool?>()))
            .Returns(listenerLogFile);

        _logs
            .Setup(x => x.Create($"device-{DeviceName}-mocked_timestamp.log", LogType.SystemLog.ToString(), It.IsAny<bool?>()))
            .Returns(deviceSystemLog.Object);

        // Act
        var appTester = new AppTester(_processManager.Object,
            _listenerFactory.Object,
            _snapshotReporterFactory,
            Mock.Of<ICaptureLogFactory>(),
            deviceLogCapturerFactory.Object,
            _testReporterFactory,
            new XmlResultParser(),
            _mainLog.Object,
            _logs.Object,
            _helpers.Object);

        var (result, resultMessage) = await appTester.TestApp(
            _appBundleInfo,
            new TestTargetOs(TestTarget.Device_iOS, null),
            s_mockDevice,
            null,
            extraAppArguments: new[] { "--foo=bar", "--xyz" },
            extraEnvVariables: new[] { ("appArg1", "value1") },
            timeout: TimeSpan.FromSeconds(30),
            testLaunchTimeout: TimeSpan.FromSeconds(30),
            signalAppEnd: false,
            skippedTestClasses: skippedClasses);

        // Verify
        Assert.Equal(TestExecutingResult.Succeeded, result);
        Assert.Equal("Tests run: 1194 Passed: 1191 Inconclusive: 0 Failed: 0 Ignored: 0", resultMessage);

        var skippedTestsArg = $"-setenv=NUNIT_RUN_ALL=false -setenv=NUNIT_SKIPPED_CLASSES={string.Join(',', skippedClasses)} ";
        var expectedArgs = GetExpectedDeviceMlaunchArgs(skippedTestsArg, extraArgs: "-setenv=appArg1=value1 -argument=--foo=bar -argument=--xyz ");

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

        _listener.Verify(x => x.InitializeAndGetPort(), Times.AtLeastOnce);
        _listener.Verify(x => x.StartAsync(), Times.AtLeastOnce);
        _listener.Verify(x => x.Dispose(), Times.AtLeastOnce);

        _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);

        deviceSystemLog.Verify(x => x.Dispose(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task TestOnMacCatalystTest()
    {
        var testResultFilePath = Path.GetTempFileName();
        var listenerLogFile = Mock.Of<IFileBackedLog>(x => x.FullPath == testResultFilePath);
        File.WriteAllLines(testResultFilePath, new[] { "Some result here", "Tests run: 124", "Some result there" });

        _logs
            .Setup(x => x.Create("test-maccatalyst-mocked_timestamp.log", "TestLog", It.IsAny<bool?>()))
            .Returns(listenerLogFile);

        var captureLog = new Mock<ICaptureLog>();
        captureLog.SetupGet(x => x.FullPath).Returns(_simulatorLogPath);

        var captureLogFactory = new Mock<ICaptureLogFactory>();
        captureLogFactory
            .Setup(x => x.Create(
               It.IsAny<string>(),
               "/var/log/system.log",
               false,
               It.IsAny<LogType>()))
            .Returns(captureLog.Object);

        // Act
        var appTester = new AppTester(
            _processManager.Object,
            _listenerFactory.Object,
            _snapshotReporterFactory,
            captureLogFactory.Object,
            Mock.Of<IDeviceLogCapturerFactory>(),
            _testReporterFactory,
            new XmlResultParser(),
            _mainLog.Object,
            _logs.Object,
            _helpers.Object);

        var (result, resultMessage) = await appTester.TestMacCatalystApp(
            _appBundleInfo,
            timeout: TimeSpan.FromSeconds(30),
            testLaunchTimeout: TimeSpan.FromSeconds(30),
            signalAppEnd: false,
            extraAppArguments: new[] { "--foo=bar", "--xyz" },
            extraEnvVariables: new[] { ("appArg1", "value1") });

        // Verify
        Assert.Equal(TestExecutingResult.Succeeded, result);
        Assert.Equal("Tests run: 1194 Passed: 1191 Inconclusive: 0 Failed: 0 Ignored: 0", resultMessage);

        _processManager
            .Verify(
                x => x.ExecuteCommandAsync(
                   "open",
                   It.Is<IList<string>>(args => args.Contains(s_appPath) && args.Contains("--foo=bar") && args.Contains("--foo=bar")),
                   _mainLog.Object,
                   It.IsAny<ILog>(),
                   It.IsAny<ILog>(),
                   It.IsAny<TimeSpan>(),
                   It.Is<Dictionary<string, string>>(envVars =>
                        envVars["NUNIT_HOSTNAME"] == "127.0.0.1" &&
                        envVars["NUNIT_HOSTPORT"] == Port.ToString() &&
                        envVars["NUNIT_AUTOEXIT"] == "true" &&
                        envVars["NUNIT_XML_VERSION"] == "xUnit" &&
                        envVars["NUNIT_ENABLE_XML_OUTPUT"] == "true"),
                   It.IsAny<CancellationToken>()),
                Times.Once);

        _listener.Verify(x => x.InitializeAndGetPort(), Times.AtLeastOnce);
        _listener.Verify(x => x.StartAsync(), Times.AtLeastOnce);
        _listener.Verify(x => x.Dispose(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task TestOnDeviceWithAppEndSignalTest()
    {
        var deviceSystemLog = new Mock<IFileBackedLog>();
        deviceSystemLog.SetupGet(x => x.FullPath).Returns(Path.GetTempFileName());

        var deviceLogCapturer = new Mock<IDeviceLogCapturer>();

        var deviceLogCapturerFactory = new Mock<IDeviceLogCapturerFactory>();
        deviceLogCapturerFactory
            .Setup(x => x.Create(_mainLog.Object, deviceSystemLog.Object, DeviceName))
            .Returns(deviceLogCapturer.Object);

        var testResultFilePath = Path.GetTempFileName();
        var listenerLogFile = Mock.Of<IFileBackedLog>(x => x.FullPath == testResultFilePath);
        File.WriteAllLines(testResultFilePath, new[] { "Some result here", "Tests run: 124", "Some result there" });

        _logs
            .Setup(x => x.Create("test-ios-device-mocked_timestamp.log", "TestLog", It.IsAny<bool?>()))
            .Returns(listenerLogFile);

        _logs
            .Setup(x => x.Create($"device-{DeviceName}-mocked_timestamp.log", LogType.SystemLog.ToString(), It.IsAny<bool?>()))
            .Returns(deviceSystemLog.Object);

        _tunnelBore.Setup(t => t.Create(DeviceName, It.IsAny<ILog>()));
        _listenerFactory
            .Setup(f => f.UseTunnel)
            .Returns(true);

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
        var appTester = new AppTester(
            _processManager.Object,
            _listenerFactory.Object,
            _snapshotReporterFactory,
            Mock.Of<ICaptureLogFactory>(),
            deviceLogCapturerFactory.Object,
            _testReporterFactory,
            new XmlResultParser(),
            _mainLog.Object,
            _logs.Object,
            _helpers.Object);

        var testTask = appTester.TestApp(
            _appBundleInfo,
            new TestTargetOs(TestTarget.Device_iOS, null),
            s_mockDevice,
            null,
            timeout: TimeSpan.FromMinutes(30),
            testLaunchTimeout: TimeSpan.FromMinutes(30),
            signalAppEnd: true,
            Array.Empty<string>(),
            Array.Empty<(string, string)>());

        // Everything should hang now since we mimicked mlaunch not being able to tell the app quits
        // We will wait for XHarness to kick off the mlaunch (the app)
        Assert.False(testTask.IsCompleted);
        await Task.WhenAny(appStarted.Task, Task.Delay(1000));

        // XHarness should still be running
        Assert.False(testTask.IsCompleted);

        // mlaunch should be started
        Assert.True(appStarted.Task.IsCompleted);

        // We will mimick the app writing the end signal
        var appLog = appOutputLogs.First();
        appLog.WriteLine(testEndSignal.ToString());

        // AppTester should now complete fine but we safe guard it to be sure
        await Task.WhenAny(testTask, Task.Delay(10000));

        Assert.True(testTask.IsCompleted, "Test tag wasn't detected");

        var (result, resultMessage) = await testTask;

        // Verify
        Assert.Equal(TestExecutingResult.Succeeded, result);
        Assert.Equal("Tests run: 1194 Passed: 1191 Inconclusive: 0 Failed: 0 Ignored: 0", resultMessage);

        var expectedArgs = GetExpectedDeviceMlaunchArgs(
            useTunnel: true,
            extraArgs: $"-setenv=RUN_END_TAG={testEndSignal} ");

        Assert.Equal(mlaunchArguments.Last().AsCommandLine(), expectedArgs);

        _listener.Verify(x => x.InitializeAndGetPort(), Times.AtLeastOnce);
        _listener.Verify(x => x.StartAsync(), Times.AtLeastOnce);
        _listener.Verify(x => x.Dispose(), Times.AtLeastOnce);

        // verify that we do close the tunnel when it was used
        // we dont want to leak a process
        _tunnelBore.Verify(t => t.Close(s_mockDevice.DeviceIdentifier));

        _snapshotReporter.Verify(x => x.StartCaptureAsync(), Times.AtLeastOnce);

        deviceSystemLog.Verify(x => x.Dispose(), Times.AtLeastOnce);
    }

    private string GetExpectedDeviceMlaunchArgs(string? skippedTests = null, bool useTunnel = false, string? extraArgs = null) =>
        "-setenv=NUNIT_AUTOEXIT=true " +
        $"-setenv=NUNIT_HOSTPORT={Port} " +
        "-setenv=NUNIT_ENABLE_XML_OUTPUT=true " +
        "-setenv=NUNIT_XML_VERSION=xUnit " +
        skippedTests +
        extraArgs +
        "-setenv=NUNIT_HOSTNAME=127.0.0.1,::1 " +
        "--disable-memory-limits " +
        $"--devname {s_mockDevice.DeviceIdentifier} " +
        (useTunnel ? "-setenv=USE_TCP_TUNNEL=true " : null) +
        $"--launchdevbundleid {AppBundleIdentifier} " +
        "--wait-for-exit";

    private string GetExpectedSimulatorMlaunchArgs() =>
        "-setenv=NUNIT_AUTOEXIT=true " +
        $"-setenv=NUNIT_HOSTPORT={Port} " +
        "-setenv=NUNIT_ENABLE_XML_OUTPUT=true " +
        "-setenv=NUNIT_XML_VERSION=xUnit " +
        "-setenv=appArg1=value1 " +
        "-argument=--foo=bar " +
        "-argument=--xyz " +
        "-setenv=NUNIT_HOSTNAME=127.0.0.1 " +
        $"--device=:v2:udid={_mockSimulator.UDID} " +
        $"--launchsimbundleid={AppBundleIdentifier}";
}
