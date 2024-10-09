// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.AndroidHeadless;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.AndroidHeadless;

internal class AndroidHeadlessUninstallCommand : AndroidCommand<AndroidHeadlessUninstallCommandArguments>
{
    protected override AndroidHeadlessUninstallCommandArguments Arguments { get; } = new();

    protected override string CommandUsage { get; } = "android-headless uninstall --test-folder=... [OPTIONS]";

    private const string CommandHelp = "Uninstall a test folder from an Android device";
    protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

    public AndroidHeadlessUninstallCommand() : base("uninstall", false, CommandHelp)
    {
    }

    protected override ExitCode InvokeCommand(ILogger logger)
    {
        using (logger.BeginScope("Find device where to uninstall folder"))
        {
            // Make sure the adb server is started
            var runner = new AdbRunner(logger);
            runner.StartAdbServer();

            AndroidDevice? device = runner.GetSingleDevice(
                loadArchitecture: true,
                loadApiVersion: true,
                requiredDeviceId: Arguments.DeviceId,
                requiredInstalledApp: "filename:" + Arguments.TestPath);

            if (device is null)
            {
                return ExitCode.DEVICE_NOT_FOUND;
            }

            DiagnosticsData.CaptureDeviceInfo(device);

            logger.LogDebug($"Working with {device.DeviceSerial} (API {device.ApiVersion})");

            runner.DeleteHeadlessFolder(Arguments.TestPath);
            runner.DeleteHeadlessFolder("runtime");
            return ExitCode.SUCCESS;
        }
    }
}
