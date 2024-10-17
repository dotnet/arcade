// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.XHarness.Android.Execution;
using Microsoft.DotNet.XHarness.Common.Utilities;
using Microsoft.Extensions.Logging;

using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Android.Tests;

public class AdbRunnerTests : IDisposable
{
    private static readonly string s_scratchAndOutputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private static readonly string s_adbPath = Path.Combine(s_scratchAndOutputPath, "adb");
    private static string s_currentDeviceSerial = "";
    private static int s_bootCompleteCheckTimes = 0;
    private readonly Mock<ILogger> _mainLog;
    private readonly Mock<IAdbProcessManager> _processManager;
    private readonly List<AndroidDevice> _fakeDeviceList;

    public AdbRunnerTests()
    {
        _mainLog = new Mock<ILogger>();

        _processManager = new Mock<IAdbProcessManager>();

        // Fake devices to pretend are attached to the system
        _fakeDeviceList = InitializeFakeDeviceList();

        // Fake ADB executable since its path is checked 
        Directory.CreateDirectory(s_scratchAndOutputPath);
        File.WriteAllText(s_adbPath, string.Empty);

        // Mock to check the args ADB actually gets called with
        _processManager.Setup(pm => pm.Run(
           It.IsAny<string>(), // process, not checking the value to match any call
           It.IsAny<IEnumerable<string>>(), // same
           It.IsAny<TimeSpan>())).Returns((string p, IEnumerable<string> a, TimeSpan t) => CallFakeProcessManager(p, a.ToArray(), t));
    }

    public void Dispose()
    {
        Directory.Delete(s_scratchAndOutputPath, true);
        GC.SuppressFinalize(this);
    }

    #region Tests

    [Fact]
    public void GetAdbState()
    {
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        string result = runner.GetAdbState();
        VerifyAdbCall("get-state");
        Assert.Equal("device", result);
    }

    [Fact]
    public void ClearAdbLog()
    {
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        runner.ClearAdbLog();
        VerifyAdbCall("logcat", "-b", "all", "-c");
    }
    [Fact]
    public void DumpAdbLog()
    {
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        string pathToDumpLogTo = Path.Join(s_scratchAndOutputPath, $"{Path.GetRandomFileName()}.log");
        runner.TryDumpAdbLog(pathToDumpLogTo);
        VerifyAdbCall("logcat", "-d", "");

        Assert.Equal("Sample LogCat Output", File.ReadAllText(pathToDumpLogTo));
    }

    [Fact]
    public void DumpBugReport()
    {
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        string pathToDumpBugReport = Path.Join(s_scratchAndOutputPath, Path.GetRandomFileName());
        runner.GetDevice(requiredDeviceId: _fakeDeviceList.First().DeviceSerial);
        runner.DumpBugReport(pathToDumpBugReport);
        VerifyAdbCall("bugreport", $"{pathToDumpBugReport}.zip");

        Assert.Equal("Sample BugReport Output", File.ReadAllText($"{pathToDumpBugReport}.zip"));
    }

    [Fact]
    public void WaitForDevice()
    {
        s_bootCompleteCheckTimes = 0; // Force simulating device is offline
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        string fakeDeviceName = $"emulator-{new Random().Next(9999)}";
        runner.SetActiveDevice(new AndroidDevice(fakeDeviceName));
        runner.WaitForDevice();

        s_bootCompleteCheckTimes = 0; // Force simulating device is offline
        runner.SetActiveDevice(null);
        runner.WaitForDevice();
        VerifyAdbCall(Times.Exactly(2), "wait-for-device");
        VerifyAdbCall(Times.Exactly(2), "-s", fakeDeviceName, "shell", "getprop", "sys.boot_completed");
        VerifyAdbCall(Times.Exactly(2), "shell", "getprop", "sys.boot_completed");
    }

    [Fact]
    public void ListDevicesAndArchitectures()
    {
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        var result = runner.GetDevices();
        VerifyAdbCall("devices", "-l");

        // Ensure it called, parsed the four random device names and found all four architectures
        foreach (var fakeDevice in _fakeDeviceList)
        {
            VerifyAdbCall("-s", fakeDevice.DeviceSerial, "shell", "getprop", "ro.product.cpu.abilist");
            Assert.Equal(fakeDevice.SupportedArchitectures, result.Single(d => d.DeviceSerial == fakeDevice.DeviceSerial).SupportedArchitectures);

            VerifyAdbCall("-s", fakeDevice.DeviceSerial, "shell", "getprop", "ro.build.version.sdk");
            Assert.Equal(fakeDevice.ApiVersion, result.Single(d => d.ApiVersion == fakeDevice.ApiVersion).ApiVersion);

            VerifyAdbCall("-s", fakeDevice.DeviceSerial, "shell", "getprop", "ro.product.cpu.abi");
            Assert.Equal(fakeDevice.Architecture, result.Single(d => d.DeviceSerial == fakeDevice.DeviceSerial).Architecture);
        }

        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void StartAdbServer()
    {
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        runner.StartAdbServer();
        VerifyAdbCall("start-server");
    }

    [Fact]
    public void KillAdbServer()
    {
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        runner.KillAdbServer();
        VerifyAdbCall("kill-server");
    }

    [Fact]
    public void InstallApk()
    {
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        string fakeApkPath = Path.Join(s_scratchAndOutputPath, $"{Path.GetRandomFileName()}.apk");
        File.Create(fakeApkPath).Close();
        int exitCode = runner.InstallApk(fakeApkPath);
        VerifyAdbCall("install", fakeApkPath);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void UninstallApk()
    {
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        string fakeApkName = $"{Path.GetRandomFileName()}";
        int exitCode = runner.UninstallApk(fakeApkName);
        VerifyAdbCall("uninstall", fakeApkName);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void KillApk()
    {
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        string fakeApkName = $"{Path.GetRandomFileName()}";
        int exitCode = runner.KillApk(fakeApkName);
        VerifyAdbCall("shell", "am", "kill", "--user", "all", fakeApkName);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void GetDevice()
    {
        var requiredArchitecture = "x86_64";
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        var result = runner.GetDevice(requiredArchitectures: new[] { requiredArchitecture });
        VerifyAdbCall("devices", "-l");
        Assert.Contains(_fakeDeviceList, d => d.DeviceSerial == result.DeviceSerial);
    }

    [Fact]
    public void GetDeviceWithArchitecture()
    {
        var requiredArchitecture = "x86";
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        var result = runner.GetDevice(loadArchitecture: true, requiredArchitectures: new[] { requiredArchitecture });
        VerifyAdbCall("devices", "-l");
        VerifyAdbCall("-s", result.DeviceSerial, "shell", "getprop", "ro.product.cpu.abi");
        Assert.Contains(_fakeDeviceList, d => d.DeviceSerial == result.DeviceSerial && d.Architecture == result.Architecture);
    }

    [Fact]
    public void GetDeviceWithApiVersion()
    {
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        var device = _fakeDeviceList.Single(d => d.ApiVersion == 30);
        var result = runner.GetDevice(loadArchitecture: true, requiredApiVersion: 30);
        VerifyAdbCall("devices", "-l");
        Assert.Equal(device.DeviceSerial, result.DeviceSerial);
        Assert.Equal(device.ApiVersion, result.ApiVersion);
        Assert.Equal(device.Architecture, result.Architecture);
    }

    [Fact]
    public void GetDeviceWithAppAndApiVersion()
    {
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        var device = _fakeDeviceList.Single(d => d.ApiVersion == 31 && d.InstalledApplications.Contains("net.dot.E"));
        var result = runner.GetDevice(requiredInstalledApp: "net.dot.E", requiredApiVersion: 31);
        VerifyAdbCall("devices", "-l");
        Assert.Equal(device.DeviceSerial, result.DeviceSerial);
        Assert.Equal(device.ApiVersion, result.ApiVersion);
    }

    [Fact]
    public void RebootAndroidDevice()
    {
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);
        runner.RebootAndroidDevice();
        VerifyAdbCall("reboot");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("FakeInstrumentationName")]
    public void RunInstrumentation(string instrumentationName)
    {
        string fakeApkName = Path.GetRandomFileName();
        var runner = new AdbRunner(_mainLog.Object, _processManager.Object, s_adbPath);

        ProcessExecutionResults result;
        var fakeArgs = new Dictionary<string, string>()
            {
                { "arg1", "value1" },
                { "arg2", "value2" }
            };

        result = runner.RunApkInstrumentation(fakeApkName, instrumentationName, fakeArgs, TimeSpan.FromSeconds(123));
        Assert.Equal(0, result.ExitCode);

        result = runner.RunApkInstrumentation(fakeApkName, instrumentationName, new Dictionary<string, string>(), TimeSpan.FromSeconds(456));
        Assert.Equal(0, result.ExitCode);

        if (string.IsNullOrEmpty(instrumentationName))
        {
            VerifyAdbCall("shell", "am", "instrument", "-e", "arg1", "value1", "-e", "arg2", "value2", "-w", fakeApkName);
            VerifyAdbCall("shell", "am", "instrument", "-w", fakeApkName);
        }
        else
        {
            VerifyAdbCall("shell", "am", "instrument", "-e", "arg1", "value1", "-e", "arg2", "value2", "-w", $"{fakeApkName}/{instrumentationName}");
            VerifyAdbCall("shell", "am", "instrument", "-w", $"{fakeApkName}/{instrumentationName}");
        }
    }

    #endregion

    #region Helper Functions

    // Generates a list of fake devices, one per supported architecture so we can test AdbRunner's parsing of the output.
    // As with most of these tests, if adb.exe changes, this will break (we are locked into specific version) 
    private static List<AndroidDevice> InitializeFakeDeviceList()
    {
        var r = new Random();
        return new List<AndroidDevice>
        {
            new AndroidDevice($"somedevice-{r.Next(9999)}")
            {
                ApiVersion = 29,
                Architecture = "x86_64",
                SupportedArchitectures = new[] { "x86_64", "x86" },
                InstalledApplications = new[] { "net.dot.A", "net.dot.B" }
            },

            new AndroidDevice($"somedevice-{r.Next(9999)}")
            {
                ApiVersion = 30,
                Architecture = "x86",
                SupportedArchitectures = new[] { "x86" },
                InstalledApplications = new[] { "net.dot.C", "net.dot.D" }
            },

            new AndroidDevice($"emulator-{r.Next(9999)}")
            {
                ApiVersion = 31,
                Architecture = "arm64-v8a",
                SupportedArchitectures = new[] { "arm64-v8a", "x86_64", "x86" },
                InstalledApplications = new[] { "net.dot.E", "net.dot.F" }
            },

            new AndroidDevice($"emulator-{r.Next(9999)}")
            {
                ApiVersion = 32,
                Architecture = "armeabi-v7a",
                SupportedArchitectures = new[] { "armeabi-v7a", "x86_64", "x86" },
                InstalledApplications = new[] { "net.dot.G", "net.dot.H" }
            },
        };
    }

    private ProcessExecutionResults CallFakeProcessManager(string process, string[] arguments, TimeSpan timeout)
    {
        if (Debugger.IsAttached)
        {
            Debug.WriteLine($"Fake ADB Process Manager invoked with args: '{process} {StringUtils.FormatArguments(arguments)}' (timeout = {timeout.TotalSeconds})");
        }

        bool timedOut = false;
        int exitCode = 0;
        string stdOut = "";
        string stdErr = "";
        int argStart = 0;

        if (arguments[0] == "-s")
        {
            s_currentDeviceSerial = arguments[1];
            argStart = 2;
        }

        switch (arguments[argStart].ToLowerInvariant())
        {
            case "get-state":
                stdOut = "device";
                exitCode = 0;
                break;

            case "devices":
                var s = new StringBuilder();
                int transportId = 1;
                s.AppendLine("List of devices attached");

                foreach (var device in _fakeDeviceList)
                {
                    string state = device == _fakeDeviceList.Last() ? "offline" : "online";
                    s.AppendLine($"{device.DeviceSerial}          {state} transportid:{transportId++}");
                }

                stdOut = s.ToString();
                break;

            case "shell":
                if ($"{arguments[argStart + 1]} {arguments[argStart + 2]}".Equals("getprop ro.product.cpu.abilist"))
                {
                    stdOut = string.Join(",", _fakeDeviceList.Single(d => d.DeviceSerial == s_currentDeviceSerial).SupportedArchitectures);
                }

                if ($"{arguments[argStart + 1]} {arguments[argStart + 2]}".Equals("getprop ro.product.cpu.abi"))
                {
                    stdOut = _fakeDeviceList.Single(d => d.DeviceSerial == s_currentDeviceSerial).Architecture;
                }

                if ($"{arguments[argStart + 1]} {arguments[argStart + 2]}".Equals("getprop ro.build.version.sdk"))
                {
                    stdOut = _fakeDeviceList.Single(d => d.DeviceSerial == s_currentDeviceSerial).ApiVersion + Environment.NewLine;
                }

                if ($"{arguments[argStart + 1]} {arguments[argStart + 2]}".Equals("getprop sys.boot_completed"))
                {
                    // Newline is strange, but this is actually what it looks like
                    if (s_bootCompleteCheckTimes > 0)
                    {
                        // Tell it we've booted.
                        stdOut = $"1{Environment.NewLine}";
                    }
                    else
                    {
                        stdOut = Environment.NewLine;
                    }
                    s_bootCompleteCheckTimes++;
                }

                if (string.Join(" ", arguments.Skip(argStart).Take(5)).Equals("shell pm list packages -3"))
                {
                    stdOut = "package:" + string.Join("\npackage:", _fakeDeviceList.Single(d => d.DeviceSerial == s_currentDeviceSerial).InstalledApplications);
                }

                exitCode = 0;
                break;

            case "logcat":
                if (arguments[argStart + 1].Equals("-d"))
                {
                    stdOut = "Sample LogCat Output";
                }

                break;

            case "bugreport":
                var outputPath = arguments[argStart + 1];
                File.WriteAllText(outputPath, "Sample BugReport Output");
                break;

            case "install":
            case "reboot":
            case "uninstall":
            case "wait-for-device":
            case "start-server":
            case "kill-server":
                break;

            default:
                throw new InvalidOperationException($"Fake ADB doesn't know how to handle argument: {string.Join(" ", arguments)}");
        }

        return new ProcessExecutionResults
        {
            TimedOut = timedOut,
            ExitCode = exitCode,
            StandardError = stdErr,
            StandardOutput = stdOut
        };
    }

    private void VerifyAdbCall(params string[] arguments) => VerifyAdbCall(Times.Once(), arguments);

    private void VerifyAdbCall(Times occurence, params string[] arguments)
    {
        _processManager.Verify(
            x => x.Run(s_adbPath, It.Is<IEnumerable<string>>(args => Enumerable.SequenceEqual(arguments, args)), It.IsAny<TimeSpan>()),
            occurence);
    }

    #endregion
}
