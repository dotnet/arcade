// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Android;

internal class AndroidUninstallCommand : AndroidCommand<AndroidUninstallCommandArguments>
{
    protected override AndroidUninstallCommandArguments Arguments { get; } = new();

    protected override string CommandUsage { get; } = "android uninstall --package-name=... [OPTIONS]";

    private const string CommandHelp = "Uninstall an .apk from an Android device";
    protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

    public AndroidUninstallCommand() : base("uninstall", false, CommandHelp)
    {
    }

    protected override ExitCode InvokeCommand(ILogger logger)
    {
        using (logger.BeginScope("Find device where to uninstall APK"))
        {
            // Make sure the adb server is started
            var runner = new AdbRunner(logger);
            runner.StartAdbServer();

            AndroidDevice? device = runner.GetSingleDevice(
                loadArchitecture: true,
                loadApiVersion: true,
                requiredDeviceId: Arguments.DeviceId,
                requiredInstalledApp: "package:" + Arguments.PackageName);

            if (device is null)
            {
                return ExitCode.DEVICE_NOT_FOUND;
            }

            DiagnosticsData.CaptureDeviceInfo(device);

            logger.LogDebug($"Working with {device.DeviceSerial} (API {device.ApiVersion})");

            runner.UninstallApk(Arguments.PackageName);
            return ExitCode.SUCCESS;
        }
    }
}
