// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Android;

internal class AndroidDeviceCommand : AndroidCommand<AndroidDeviceCommandArguments>
{
    protected override AndroidDeviceCommandArguments Arguments { get; } = new()
    {
        Verbosity = new VerbosityArgument(LogLevel.Error)
    };

    protected override string CommandUsage { get; } = "android device [OPTIONS]";

    private const string CommandHelp = "Get ID of the device compatible with a given .apk / architecture";
    protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

    public AndroidDeviceCommand() : base("device", false, CommandHelp)
    {
    }

    protected override ExitCode InvokeCommand(ILogger logger)
    {
        IEnumerable<string>? apkRequiredArchitecture = null;

        if (Arguments.DeviceArchitecture.Value.Any())
        {
            apkRequiredArchitecture = Arguments.DeviceArchitecture.Value;
        }
        else if (!string.IsNullOrEmpty(Arguments.AppPackagePath.Value))
        {
            if (!File.Exists(Arguments.AppPackagePath.Value))
            {
                logger.LogCritical($"Couldn't find {Arguments.AppPackagePath.Value}!");
                return ExitCode.PACKAGE_NOT_FOUND;
            }

            apkRequiredArchitecture = ApkHelper.GetApkSupportedArchitectures(Arguments.AppPackagePath.Value);
        }

        // Make sure the adb server is started
        var runner = new AdbRunner(logger);
        runner.StartAdbServer();

        // enumerate the devices attached and their architectures
        // Tell ADB to only use that one (will always use the present one for systems w/ only 1 machine)
        var device = runner.GetDevice(
            loadApiVersion: true,
            loadArchitecture: true,
            requiredApiVersion: Arguments.ApiVersion.Value,
            requiredArchitectures: apkRequiredArchitecture);

        if (device is null)
        {
            return ExitCode.DEVICE_NOT_FOUND;
        }

        DiagnosticsData.CaptureDeviceInfo(device);

        Console.WriteLine(device.DeviceSerial);

        return ExitCode.SUCCESS;
    }
}
