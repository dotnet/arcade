// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple;

internal class AppleStateCommand : GetStateCommand<AppleStateCommandArguments>
{
    protected override string CommandUsage { get; } = "ios state [OPTIONS]";

    private class DeviceInfo
    {
        public string Name { get; }
        public string UDID { get; }
        public string Type { get; }
        public string OSVersion { get; }
        public bool IsPaired { get; }

        public DeviceInfo(string name, string uDID, string type, string oSVersion, bool isPaired = true)
        {
            Name = name;
            UDID = uDID;
            Type = type;
            OSVersion = oSVersion;
            IsPaired = isPaired;
        }
    }

    private class SystemInfo
    {
        public string MachineName { get; }
        public string OSName { get; }
        public string OSVersion { get; }
        public string OSPlatform { get; }
        public string XcodePath { get; }
        public string XcodeVersion { get; }
        public string MlaunchPath { get; }
        public string MlaunchVersion { get; }
        public List<DeviceInfo> Simulators { get; } = new List<DeviceInfo>();
        public List<DeviceInfo> Devices { get; } = new List<DeviceInfo>();

        public SystemInfo(string machineName, string oSName, string oSVersion, string oSPlatform, string xcodePath, string xcodeVersion, string mlaunchPath, string mlaunchVersion)
        {
            MachineName = machineName;
            OSName = oSName;
            OSVersion = oSVersion;
            OSPlatform = oSPlatform;
            XcodePath = xcodePath;
            XcodeVersion = xcodeVersion;
            MlaunchPath = mlaunchPath;
            MlaunchVersion = mlaunchVersion;
        }
    }

    private const string SimulatorPrefix = "com.apple.CoreSimulator.SimDeviceType.";

    public AppleStateCommand() : base(TargetPlatform.Apple, new ServiceCollection())
    {
    }

    protected override AppleStateCommandArguments Arguments { get; } = new();

    private static async Task AsJson(SystemInfo info)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        await JsonSerializer.SerializeAsync(Console.OpenStandardOutput(), info, options);
        Console.WriteLine();
    }

    private void AsText(SystemInfo info)
    {
        Console.WriteLine("Runtime Enviroment:");
        Console.WriteLine($"  Machine name:\t{info.MachineName}");
        Console.WriteLine($"  OS Name:\t{info.OSName}");
        Console.WriteLine($"  OS Version:\t{info.OSVersion}");
        Console.WriteLine($"  OS Platform:\t{info.OSPlatform}");

        Console.WriteLine();

        Console.WriteLine("Developer Tools:");

        Console.WriteLine($"  Xcode:\t{info.XcodeVersion} - {info.XcodePath}");
        Console.WriteLine($"  Mlaunch:\t{info.MlaunchVersion} - {info.MlaunchPath}");

        Console.WriteLine();

        Console.WriteLine("Installed Simulators:");

        if (info.Simulators.Any())
        {
            var maxLength = info.Simulators.Select(s => s.Name.Length).Max();

            foreach (var sim in info.Simulators)
            {
                var uuid = Arguments.ShowSimulatorsUUID ? $"{sim.UDID} " : string.Empty;
                Console.WriteLine($"  {uuid}{sim.Name.PadRight(maxLength)} {sim.OSVersion,-13} {sim.Type}");
            }
        }
        else
        {
            Console.WriteLine("  none");
        }

        Console.WriteLine();

        Console.WriteLine("Connected Devices:");

        if (info.Devices.Any())
        {
            var maxLength = info.Devices.Select(s => s.Name.Length).Max();

            foreach (var dev in info.Devices)
            {
                var uuid = Arguments.ShowDevicesUUID ? $" {dev.UDID}   " : "";
                var notPaired = dev.IsPaired ? "" : "(not paired) ";
                Console.WriteLine($"  {notPaired}{dev.Name.PadRight(maxLength)}{uuid} {dev.OSVersion,-13} {dev.Type}");
            }
        }
        else
        {
            Console.WriteLine("  none");
        }
    }

    protected override async Task<ExitCode> InvokeInternal(Extensions.Logging.ILogger logger)
    {
        var processManager = new MlaunchProcessManager(xcodeRoot: Arguments.XcodeRoot, mlaunchPath: Arguments.MlaunchPath);
        var deviceLoader = new HardwareDeviceLoader(processManager);
        var simulatorLoader = new SimulatorLoader(processManager);
        var log = new MemoryLog(); // do we really want to log this?

        var mlaunchLog = new MemoryLog { Timestamp = false };

        ProcessExecutionResult result;

        try
        {
            result = await processManager.ExecuteCommandAsync(new MlaunchArguments(new MlaunchVersionArgument()), new NullLog(), mlaunchLog, new NullLog(), TimeSpan.FromSeconds(10));
        }
        catch (Exception e)
        {
            logger.LogError($"Failed to get mlaunch version info:{Environment.NewLine}{e}");
            return ExitCode.GENERAL_FAILURE;
        }

        if (!result.Succeeded)
        {
            logger.LogError($"Failed to get mlaunch version info:{Environment.NewLine}{mlaunchLog}");
            return ExitCode.GENERAL_FAILURE;
        }

        // build the required data, then depending on the format print out
        var info = new SystemInfo(
            machineName: Environment.MachineName,
            oSName: "Mac OS X",
            oSVersion: Darwin.GetVersion() ?? "",
            oSPlatform: "Darwin",
            xcodePath: processManager.XcodeRoot,
            xcodeVersion: processManager.XcodeVersion.ToString(),
            mlaunchPath: processManager.MlaunchPath,
            mlaunchVersion: mlaunchLog.ToString().Trim());

        try
        {
            await simulatorLoader.LoadDevices(log);
        }
        catch (Exception e)
        {
            logger.LogError($"Failed to load simulators:{Environment.NewLine}{e}");
            logger.LogInformation($"Execution log:{Environment.NewLine}{log}");
            return ExitCode.GENERAL_FAILURE;
        }

        foreach (var sim in simulatorLoader.AvailableDevices)
        {
            info.Simulators.Add(new DeviceInfo(
                name: sim.Name,
                uDID: sim.UDID,
                type: sim.SimDeviceType.Remove(0, SimulatorPrefix.Length).Replace('-', ' '),
                oSVersion: sim.OSVersion));
        }

        try
        {
            await deviceLoader.LoadDevices(log, includeWirelessDevices: Arguments.IncludeWireless);
        }
        catch (Exception e)
        {
            logger.LogError($"Failed to load connected devices:{Environment.NewLine}{e}");
            logger.LogInformation($"Execution log:{Environment.NewLine}{log}");
            return ExitCode.GENERAL_FAILURE;
        }

        foreach (var dev in deviceLoader.ConnectedDevices)
        {
            info.Devices.Add(new DeviceInfo(
                name: dev.Name,
                uDID: dev.DeviceIdentifier,
                type: $"{dev.DeviceClass} {dev.DevicePlatform}",
                oSVersion: dev.OSVersion,
                isPaired: dev.IsPaired));
        }

        if (Arguments.UseJson)
        {
            await AsJson(info);
        }
        else
        {
            AsText(info);
        }

        return ExitCode.SUCCESS;
    }

    private class MlaunchVersionArgument : OptionArgument
    {
        public MlaunchVersionArgument() : base("version")
        {
        }
    }
}
