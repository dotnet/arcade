using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.AndroidHeadless;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.AndroidHeadless;

internal class AndroidHeadlessInstallCommand : AndroidCommand<AndroidHeadlessInstallCommandArguments>
{
    protected override AndroidHeadlessInstallCommandArguments Arguments { get; } = new();

    protected override string CommandUsage { get; } = "android-headless install --test-folder=... [OPTIONS]";

    private const string CommandHelp = "Install a test folder to an Android device without running it";
    protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

    public AndroidHeadlessInstallCommand() : base("install", false, CommandHelp)
    {
    }

    protected override ExitCode InvokeCommand(ILogger logger)
    {
        if (!Directory.Exists(Arguments.TestPath))
        {
            logger.LogCritical($"Couldn't find test {Arguments.TestPath}!");
            return ExitCode.PACKAGE_NOT_FOUND;
        }

        if (!Directory.Exists(Arguments.RuntimePath))
        {
            logger.LogCritical($"Couldn't find shared runtime {Arguments.RuntimePath}!");
            return ExitCode.PACKAGE_NOT_FOUND;
        }

        var runner = new AdbRunner(logger);

        List<string> testRequiredArchitecture = new();

        if (string.IsNullOrEmpty(Arguments.DeviceId))
        {
            // trying to choose suitable device
            if (Arguments.DeviceArchitecture.Value.Any())
            {
                testRequiredArchitecture = Arguments.DeviceArchitecture.Value.ToList();
                logger.LogInformation($"Will attempt to run device on specified architecture: '{string.Join("', '", testRequiredArchitecture)}'");
            }
        }

        return InvokeHelper(
            logger: logger,
            testPath: Arguments.TestPath,
            runtimePath: Arguments.RuntimePath,
            testRequiredArchitecture: testRequiredArchitecture,
            deviceId: Arguments.DeviceId,
            apiVersion: Arguments.ApiVersion.Value,
            bootTimeoutSeconds: Arguments.LaunchTimeout,
            runner: runner,
            DiagnosticsData);
    }

    public static ExitCode InvokeHelper(
        ILogger logger,
        string testPath,
        string runtimePath,
        IEnumerable<string> testRequiredArchitecture,
        string? deviceId,
        int? apiVersion,
        TimeSpan bootTimeoutSeconds,
        AdbRunner runner,
        IDiagnosticsData diagnosticsData)
    {
        using (logger.BeginScope("Initialization and setup of test on device"))
        {
            // Make sure the adb server is started
            runner.StartAdbServer();

            AndroidDevice? device = runner.GetDevice(
                loadArchitecture: true,
                loadApiVersion: true,
                deviceId,
                apiVersion,
                testRequiredArchitecture);

            if (device is null)
            {
                throw new NoDeviceFoundException($"Failed to find compatible device: {string.Join(", ", testRequiredArchitecture)}");
            }

            diagnosticsData.CaptureDeviceInfo(device);

            runner.TimeToWaitForBootCompletion = bootTimeoutSeconds;

            // Wait till at least device(s) are ready
            if (!runner.WaitForDevice())
            {
                return ExitCode.DEVICE_NOT_FOUND;
            }

            logger.LogDebug($"Working with {device.DeviceSerial} (API {device.ApiVersion})");

            // If anything changed about the app, Install will fail; uninstall it first.
            // (we'll ignore if it's not present)
            // This is where mismatched architecture APKs fail.
            runner.DeleteHeadlessFolder(testPath);
            runner.DeleteHeadlessFolder("runtime");
            if (runner.CopyHeadlessFolder(testPath) != 0)
            {
                logger.LogCritical("Install failure: Test command cannot continue");
                runner.DeleteHeadlessFolder(testPath);
                return ExitCode.PACKAGE_INSTALLATION_FAILURE;
            }
            if (runner.CopyHeadlessFolder(runtimePath, true) != 0)
            {
                logger.LogCritical("Install failure: Test command cannot continue");
                runner.DeleteHeadlessFolder("runtime");
                return ExitCode.PACKAGE_INSTALLATION_FAILURE;
            }
        }
        return ExitCode.SUCCESS;
    }
}
