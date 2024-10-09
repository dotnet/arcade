// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple;

internal class AppleDeviceCommand : AppleCommand<AppleDeviceCommandArguments>
{
    protected override AppleDeviceCommandArguments Arguments { get; } = new();

    protected override string CommandUsage { get; } = "apple device [OPTIONS] [TARGET]";

    private const string CommandHelp = "Finds the UDID of a device/simulator for given target";
    protected override string CommandDescription { get; } = @$"
{CommandHelp}
 
Arguments:
";

    public AppleDeviceCommand(IServiceCollection services) : base("device", true, services, CommandHelp)
    {
    }

    protected override async Task<ExitCode> Invoke(Extensions.Logging.ILogger logger)
    {
        var log = new CallbackLog(m => logger.LogDebug(m));
        TestTargetOs target;

        try
        {
            target = ParseTarget();
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
            return ExitCode.INVALID_ARGUMENTS;
        }

        var serviceProvider = Services.BuildServiceProvider();
        var deviceFinder = serviceProvider.GetRequiredService<IDeviceFinder>();
        var diagnosticsData = serviceProvider.GetRequiredService<IDiagnosticsData>();
        diagnosticsData.Target = target.AsString();

        try
        {
            var device = (await deviceFinder.FindDevice(target, Arguments.DeviceName, log, Arguments.IncludeWireless)).Device;

            diagnosticsData.TargetOS = device.OSVersion.Split(' ', 2).Last();
            diagnosticsData.Device = device.Name ?? device.UDID;
            diagnosticsData.IsDevice = !target.Platform.IsSimulator();

            Console.WriteLine(device.UDID);
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
            return ExitCode.DEVICE_NOT_FOUND;
        }

        return ExitCode.SUCCESS;
    }

    private TestTargetOs ParseTarget()
    {
        if (ExtraArguments.Count() != 1)
        {
            throw new ArgumentException("You have to specify one target platform");
        }

        var target = new TargetArgument();
        target.Action(ExtraArguments.First());
        target.Validate();

        if (target.Value.Platform == TestTarget.MacCatalyst)
        {
            throw new ArgumentException("Target maccatalyst is not supported for this command");
        }

        return target.Value;
    }
}
