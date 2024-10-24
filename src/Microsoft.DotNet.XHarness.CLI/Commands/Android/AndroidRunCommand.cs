// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Android;

internal class AndroidRunCommand : AndroidCommand<AndroidRunCommandArguments>
{
    protected override AndroidRunCommandArguments Arguments { get; } = new();

    protected override string CommandUsage { get; } = "android run --output-directory=... --package-name=... [OPTIONS]";

    private const string CommandHelp = "Run tests using an already installed .apk on an Android device";
    protected override string CommandDescription { get; } = @$"
{CommandHelp}

APKs can communicate status back to XHarness using the parameters:

Required:
{InstrumentationRunner.ReturnCodeVariableName} - Exit code for instrumentation. Necessary because a crashing instrumentation may be indistinguishable from a passing one based solely on the exit code.
 
Arguments:
";

    public AndroidRunCommand() : base("run", false, CommandHelp)
    {
    }

    protected override ExitCode InvokeCommand(ILogger logger)
    {
        var runner = new AdbRunner(logger);

        // Make sure the adb server is started
        runner.StartAdbServer();

        var device = string.IsNullOrEmpty(Arguments.DeviceId.Value)
            ? runner.GetSingleDevice(loadArchitecture: true, loadApiVersion: true, requiredInstalledApp: "package:" + Arguments.PackageName)
            : runner.GetSingleDevice(loadArchitecture: true, loadApiVersion: true, requiredDeviceId: Arguments.DeviceId.Value);

        if (device is null)
        {
            return ExitCode.DEVICE_NOT_FOUND;
        }

        DiagnosticsData.CaptureDeviceInfo(device);

        runner.TimeToWaitForBootCompletion = Arguments.LaunchTimeout;

        // Wait till at least device(s) are ready
        if (!runner.WaitForDevice())
        {
            return ExitCode.DEVICE_NOT_FOUND;
        }

        logger.LogDebug($"Working with API {runner.GetAdbVersion()}");

        // Empty log as we'll be uploading the full logcat for this execution
        runner.ClearAdbLog();
        
        if (Arguments.Wifi != WifiStatus.Unknown)
        {
            runner.EnableWifi(Arguments.Wifi == WifiStatus.Enable);
        }

        var instrumentationRunner = new InstrumentationRunner(logger, runner);
        return instrumentationRunner.RunApkInstrumentation(
            Arguments.PackageName,
            Arguments.InstrumentationName,
            Arguments.InstrumentationArguments,
            Arguments.OutputDirectory,
            Arguments.DeviceOutputFolder,
            Arguments.Timeout,
            Arguments.ExpectedExitCode);
    }
}
