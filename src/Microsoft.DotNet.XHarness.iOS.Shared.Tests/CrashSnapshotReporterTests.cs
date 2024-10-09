// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests;

public class CrashReportSnapshotTests : IDisposable
{
    private readonly string _tempXcodeRoot;
    private readonly string _symbolicatePath;
    private readonly Mock<IMlaunchProcessManager> _processManager;
    private readonly Mock<ILog> _log;
    private readonly Mock<ILogs> _logs;

    public CrashReportSnapshotTests()
    {
        _processManager = new Mock<IMlaunchProcessManager>();
        _log = new Mock<ILog>();
        _logs = new Mock<ILogs>();

        _tempXcodeRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _symbolicatePath = Path.Combine(_tempXcodeRoot, "Contents", "SharedFrameworks", "DTDeviceKitBase.framework", "Versions", "A", "Resources");

        _processManager.SetupGet(x => x.XcodeRoot).Returns(_tempXcodeRoot);
        _processManager.SetupGet(x => x.MlaunchPath).Returns("/var/bin/mlaunch");

        // Create fake place for device logs
        Directory.CreateDirectory(_tempXcodeRoot);

        // Create fake symbolicate binary
        Directory.CreateDirectory(_symbolicatePath);
        File.WriteAllText(Path.Combine(_symbolicatePath, "symbolicatecrash"), "");
    }

    public void Dispose()
    {
        Directory.Delete(_tempXcodeRoot, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task DeviceCaptureTest()
    {
        var tempFilePath = Path.GetTempFileName();

        const string deviceName = "Sample-iPhone";
        const string crashLogPath = "/path/to/crash.log";
        const string symbolicateLogPath = "/path/to/" + deviceName + ".symbolicated.log";

        var crashReport = Mock.Of<IFileBackedLog>(x => x.FullPath == crashLogPath);
        var symbolicateReport = Mock.Of<IFileBackedLog>(x => x.FullPath == symbolicateLogPath);

        // Crash report is added
        _logs.Setup(x => x.Create(deviceName, "Crash report: " + deviceName, It.IsAny<bool>()))
            .Returns(crashReport);

        // Symbolicate report is added
        _logs.Setup(x => x.Create("crash.symbolicated.log", "Symbolicated crash report: crash.log", It.IsAny<bool>()))
            .Returns(symbolicateReport);

        _processManager.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult() { ExitCode = 0 }));

        // Act
        var snapshotReport = new CrashSnapshotReporter(_processManager.Object,
            _log.Object,
            _logs.Object,
            true,
            deviceName,
            () => tempFilePath);

        File.WriteAllLines(tempFilePath, new[] { "crash 1", "crash 2" });

        await snapshotReport.StartCaptureAsync();

        File.WriteAllLines(tempFilePath, new[] { "Sample-iPhone" });

        await snapshotReport.EndCaptureAsync(TimeSpan.FromSeconds(10));

        // Verify
        _logs.VerifyAll();

        // List of crash reports is retrieved
        _processManager.Verify(
            x => x.ExecuteCommandAsync(
                It.Is<MlaunchArguments>(args => args.AsCommandLine() ==
                   StringUtils.FormatArguments(
                       $"--list-crash-reports={tempFilePath}") + " " +
                       $"--devname {StringUtils.FormatArguments(deviceName)}"),
                _log.Object,
                TimeSpan.FromMinutes(1),
                null,
                It.IsAny<int>(),
                null),
            Times.Exactly(2));

        // Device crash log is downloaded
        _processManager.Verify(
            x => x.ExecuteCommandAsync(
                It.Is<MlaunchArguments>(args => args.AsCommandLine() ==
                    StringUtils.FormatArguments(
                        $"--download-crash-report={deviceName}") + " " +
                        StringUtils.FormatArguments($"--download-crash-report-to={crashLogPath}") + " " +
                        $"--devname {StringUtils.FormatArguments(deviceName)}"),
                _log.Object,
                TimeSpan.FromMinutes(1),
                null,
                It.IsAny<int>(),
                null),
            Times.Once);

        // Symbolicate is ran
        _processManager.Verify(
            x => x.ExecuteCommandAsync(
                Path.Combine(_symbolicatePath, "symbolicatecrash"),
                It.Is<IList<string>>(args => args.First() == crashLogPath),
                symbolicateReport,
                TimeSpan.FromMinutes(1),
                It.IsAny<Dictionary<string, string>>(),
                null),
            Times.Once);
    }
}
