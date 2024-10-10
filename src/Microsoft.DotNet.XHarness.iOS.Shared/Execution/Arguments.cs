// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Execution;

/// <summary>
/// Specify the location of Apple SDKs, default to 'xcode-select' value.
/// </summary>
public sealed class SdkRootArgument : SingleValueArgument
{
    public SdkRootArgument(string sdkPath) : base("sdkroot", sdkPath, false)
    {
    }
}

/// <summary>
/// List the currently connected devices and their UDIDs.
/// </summary>
public sealed class ListDevicesArgument : SingleValueArgument
{
    public ListDevicesArgument(string outputFile) : base("listdev", outputFile)
    {
    }
}

/// <summary>
/// When listing devices, should mlaunch also scan for wireless devices.
/// </summary>
public sealed class ListWirelessDevicesArgument : SingleValueArgument
{
    public ListWirelessDevicesArgument(bool wirelessEnabled) : base("list-wireless-devices", wirelessEnabled ? "true" : "false", true)
    {
    }
}

/// <summary>
/// Write the syslog from the device to the console.
/// </summary>
public sealed class LogDevArgument : OptionArgument
{
    public LogDevArgument() : base("logdev")
    {
    }
}

/// <summary>
/// List the available simulators. The output is xml, and written to the specified file.
/// </summary>
public sealed class ListSimulatorsArgument : SingleValueArgument
{
    public ListSimulatorsArgument(string outputFile) : base("listsim", outputFile)
    {
    }
}

/// <summary>
/// Lists crash reports on the specified device
/// </summary>
public sealed class ListCrashReportsArgument : SingleValueArgument
{
    public ListCrashReportsArgument(string outputFile) : base("list-crash-reports", outputFile)
    {
    }
}

/// <summary>
/// Specifies the device type to launch the simulator as.
/// </summary>
public sealed class DeviceArgument : SingleValueArgument
{
    public DeviceArgument(string deviceType) : base("device", deviceType)
    {
    }
}

/// <summary>
/// Specify which device (when many are present) the [install|lauch|kill|log]dev command applies.
/// </summary>
public sealed class DeviceNameArgument : SingleValueArgument
{
    private const string ArgName = "devname";

    public DeviceNameArgument(string deviceName) : base(ArgName, deviceName, false)
    {
    }

    public DeviceNameArgument(IDevice device) : base(ArgName, device.UDID, false)
    {
    }
}

/// <summary>
/// Install the specified iOS app bundle on the device.
/// </summary>
public sealed class InstallAppOnDeviceArgument : SingleValueArgument
{
    public InstallAppOnDeviceArgument(string appPath) : base("installdev", appPath, false)
    {
    }
}

/// <summary>
/// Install the specified iOS app bundle on the Simulator.
/// </summary>
public sealed class InstallAppOnSimulatorArgument : SingleValueArgument
{
    public InstallAppOnSimulatorArgument(string appPath) : base("installsim", appPath, false)
    {
    }
}

/// <summary>
/// Uninstall the specified bundle id from the device.
/// </summary>
public sealed class UninstallAppFromDeviceArgument : SingleValueArgument
{
    public UninstallAppFromDeviceArgument(string appBundleId) : base("uninstalldevbundleid", appBundleId, false)
    {
    }
}

/// <summary>
/// Specify the output format for some commands as Default.
/// </summary>
public sealed class DefaultOutputFormatArgument : SingleValueArgument
{
    public DefaultOutputFormatArgument() : base("output-format", "Default")
    {
    }
}

/// <summary>
/// Specify the output format for some commands as XML.
/// </summary>
public sealed class XmlOutputFormatArgument : SingleValueArgument
{
    public XmlOutputFormatArgument() : base("output-format", "XML")
    {
    }
}

/// <summary>
/// Download a crash report from the specified device.
/// </summary>
public sealed class DownloadCrashReportArgument : SingleValueArgument
{
    public DownloadCrashReportArgument(string deviceName) : base("download-crash-report", deviceName)
    {
    }
}

/// <summary>
/// Specifies the file to save the downloaded crash report.
/// </summary>
public sealed class DownloadCrashReportToArgument : SingleValueArgument
{
    public DownloadCrashReportToArgument(string outputFile) : base("download-crash-report-to", outputFile)
    {
    }
}

/// <summary>
/// Include additional data (which can take some time to fetch) when listing the connected devices.
/// Only applicable when output format is xml.
/// </summary>
public sealed class ListExtraDataArgument : OptionArgument
{
    public ListExtraDataArgument() : base("list-extra-data")
    {
    }
}

/// <summary>
/// Attach native debugger.
/// </summary>
public sealed class AttachNativeDebuggerArgument : OptionArgument
{
    public AttachNativeDebuggerArgument() : base("attach-native-debugger")
    {
    }
}

/// <summary>
/// Attempt to disable memory limits for launched apps.
/// This is just an attempt, some or all usual limits may still be enforced.
/// </summary>
public sealed class DisableMemoryLimitsArgument : OptionArgument
{
    public DisableMemoryLimitsArgument() : base("disable-memory-limits")
    {
    }
}

public sealed class WaitForExitArgument : OptionArgument
{
    public WaitForExitArgument() : base("wait-for-exit")
    {
    }
}

/// <summary>
/// Launch the app with this command line argument. This must be specified multiple times for multiple arguments.
/// </summary>
public sealed class SetAppArgumentArgument : MlaunchArgument
{
    private readonly string _value;

    public SetAppArgumentArgument(string value, bool isAppArg = false)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));

        if (isAppArg)
        {
            _value = "-app-arg:" + _value;
        }
    }

    public override string AsCommandLineArgument() => "-argument=" + _value;
}

/// <summary>
/// Set the environment variable in the application on startup.
/// </summary>
public sealed class SetEnvVariableArgument : MlaunchArgument
{
    private readonly string _variableName;
    private readonly string _variableValue;

    public SetEnvVariableArgument(string variableName, object variableValue)
    {
        _variableName = variableName ?? throw new ArgumentNullException(nameof(variableName));

        if (variableValue is bool b)
        {
            _variableValue = b.ToString().ToLowerInvariant();
        }
        else
        {
            _variableValue = variableValue?.ToString() ?? throw new ArgumentNullException(nameof(variableValue));
        }
    }

    public override string AsCommandLineArgument() => Escape($"-setenv={_variableName}={_variableValue}");
}

/// <summary>
/// Redirect the standard output for the simulated application to the specified file.
/// </summary>
public sealed class SetStdoutArgument : SingleValueArgument
{
    public SetStdoutArgument(string targetFile) : base("stdout", targetFile)
    {
    }
}

/// <summary>
/// Redirect the standard error for the simulated application to the specified file.
/// </summary>
public sealed class SetStderrArgument : SingleValueArgument
{
    public SetStderrArgument(string targetFile) : base("stderr", targetFile)
    {
    }
}

/// <summary>
/// Launch an app that is installed on device, specified by bundle identifier.
/// </summary>
public sealed class LaunchDeviceArgument : SingleValueArgument
{
    private const string ArgName = "launchdev";

    public LaunchDeviceArgument(string launchAppPath) : base(ArgName, launchAppPath, false)
    {
    }

    public LaunchDeviceArgument(AppBundleInformation appInfo) : base(ArgName, appInfo.AppPath, false)
    {
    }
}

/// <summary>
/// Launch an app that is installed on device, 
/// </summary>
public sealed class LaunchDeviceBundleIdArgument : SingleValueArgument
{
    private const string ArgName = "launchdevbundleid";

    public LaunchDeviceBundleIdArgument(string bundleId) : base(ArgName, bundleId, false)
    {
    }

    public LaunchDeviceBundleIdArgument(AppBundleInformation appInfo) : base(ArgName, appInfo.BundleIdentifier, false)
    {
    }
}

/// <summary>
/// Launch the specified app in the simulator.
/// </summary>
public sealed class LaunchSimulatorAppArgument : SingleValueArgument
{
    private const string ArgName = "launchsim";

    public LaunchSimulatorAppArgument(string launchAppPath) : base(ArgName, launchAppPath, false)
    {
    }

    public LaunchSimulatorAppArgument(AppBundleInformation appInfo) : base(ArgName, appInfo.AppPath, false)
    {
    }
}

/// <summary>
/// Launch the simulator only.
/// </summary>
public sealed class LaunchSimulatorArgument : OptionArgument
{
    private const string ArgName = "launchsimulator";

    public LaunchSimulatorArgument() : base(ArgName)
    {
    }
}

/// <summary>
/// Launch the specified already installed app in the simulator.
/// </summary>
public sealed class LaunchSimulatorBundleArgument : SingleValueArgument
{
    public LaunchSimulatorBundleArgument(AppBundleInformation appInfo) : base("launchsimbundleid", appInfo.BundleIdentifier, true)
    {
    }
}

/// <summary>
/// Specify which simulator to launch.
/// </summary>
public sealed class SimulatorUDIDArgument : MlaunchArgument
{
    private readonly string _udid;

    public SimulatorUDIDArgument(string udid)
    {
        _udid = udid ?? throw new ArgumentNullException(nameof(udid));
    }

    public SimulatorUDIDArgument(IDevice device)
    {
        _udid = device?.UDID ?? throw new ArgumentNullException(nameof(device));
    }

    public override string AsCommandLineArgument() => $"--device=:v2:udid={_udid}";
}

/// <summary>
/// Launch an app that is installed on device, specified by bundle identifier.
/// </summary>
public sealed class LaunchSimulatorExtensionArgument : MlaunchArgument
{
    private readonly string _launchAppPath;
    private readonly string _bundleId;

    public LaunchSimulatorExtensionArgument(string launchAppPath, string bundleId)
    {
        _launchAppPath = launchAppPath ?? throw new ArgumentNullException(nameof(launchAppPath));
        _bundleId = bundleId ?? throw new ArgumentNullException(nameof(bundleId));
    }

    public override string AsCommandLineArgument() => "--launchsimbundleid " +
        "todayviewforextensions:" + Escape(_bundleId) + " " +
        "--observe-extension " + Escape(_launchAppPath);
}

/// <summary>
/// Launch the specified bundle id in the simulator (which must already be installed).
/// </summary>
public sealed class LaunchDeviceExtensionArgument : MlaunchArgument
{
    private readonly string _launchAppPath;
    private readonly string _bundleId;

    public LaunchDeviceExtensionArgument(string launchAppPath, string bundleId)
    {
        _launchAppPath = launchAppPath ?? throw new ArgumentNullException(nameof(launchAppPath));
        _bundleId = bundleId ?? throw new ArgumentNullException(nameof(bundleId));
    }

    public override string AsCommandLineArgument() => "--launchdevbundleid " +
        "todayviewforextensions:" + Escape(_bundleId) + " " +
        "--observe-extension " + Escape(_launchAppPath);
}

/// <summary>
/// Set the verbosity level. Can be used repeatedly to lower the level.
/// </summary>
public sealed class VerbosityArgument : MlaunchArgument
{
    public VerbosityArgument()
    {
    }

    public override string AsCommandLineArgument() => "-v";
}

/// <summary>
/// Create a tcp tunnel with the iOS device from the host.
/// </summary>
public sealed class TcpTunnelArgument : MlaunchArgument
{
    private readonly int _port;

    public TcpTunnelArgument(int port)
    {
        if (port <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        _port = port;
    }

    public override string AsCommandLineArgument() => $"--tcp-tunnel={_port}:{_port}";
}

/// <summary>
/// Specify a timeout (in seconds) for the commands that doesn't have fixed duration.
/// </summary>
public sealed class TimeoutArgument : SingleValueArgument
{
    public TimeoutArgument(double timeoutInSeconds) : base("timeout", timeoutInSeconds.ToString())
    {
    }
}
