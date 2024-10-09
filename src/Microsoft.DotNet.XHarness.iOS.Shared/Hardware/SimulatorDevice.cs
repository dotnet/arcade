// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

public class SimulatorDevice : ISimulatorDevice
{
    private readonly IMlaunchProcessManager _processManager;
    private readonly ITCCDatabase _tCCDatabase;

    public string UDID { get; set; }
    public string Name { get; set; }
    public string SimRuntime { get; set; }
    public string SimDeviceType { get; set; }
    public DeviceState State { get; set; } = DeviceState.Unknown;
    public string DataPath { get; set; }
    public string LogPath { get; set; }
    public string SystemLog => Path.Combine(LogPath, "system.log");


    public SimulatorDevice(IMlaunchProcessManager processManager, ITCCDatabase tccDatabase)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _tCCDatabase = tccDatabase ?? throw new ArgumentNullException(nameof(tccDatabase));
    }

    public bool IsWatchSimulator => SimRuntime.StartsWith("com.apple.CoreSimulator.SimRuntime.watchOS", StringComparison.Ordinal);

    public string OSVersion
    {
        get
        {
            var v = SimRuntime.Substring("com.apple.CoreSimulator.SimRuntime.".Length);
            var dash = v.IndexOf('-');
            return v.Substring(0, dash) + " " + v.Substring(dash + 1).Replace('-', '.');
        }
    }

    public async Task Erase(ILog log)
    {
        // here we don't care if execution fails.
        // erase the simulator (make sure the device isn't running first)
        await _processManager.ExecuteXcodeCommandAsync("simctl", new[] { "shutdown", UDID }, log, TimeSpan.FromMinutes(1));
        await _processManager.ExecuteXcodeCommandAsync("simctl", new[] { "erase", UDID }, log, TimeSpan.FromMinutes(1));

        // boot & shutdown to make sure it actually works
        await _processManager.ExecuteXcodeCommandAsync("simctl", new[] { "boot", UDID }, log, TimeSpan.FromMinutes(1));
        await _processManager.ExecuteXcodeCommandAsync("simctl", new[] { "shutdown", UDID }, log, TimeSpan.FromMinutes(1));
    }

    public async Task Shutdown(ILog log)
    {
        await _processManager.ExecuteXcodeCommandAsync("simctl", new[] { "shutdown", UDID }, log, TimeSpan.FromMinutes(1));
        State = DeviceState.Shutdown;
    }

    public async Task KillEverything(ILog log)
    {
        await _processManager.ExecuteCommandAsync("launchctl", new[] { "remove", "com.apple.CoreSimulator.CoreSimulatorService" }, log, TimeSpan.FromSeconds(10));

        var toKill = new string[] { "iPhone Simulator", "iOS Simulator", "Simulator", "Simulator (Watch)", "com.apple.CoreSimulator.CoreSimulatorService", "ibtoold" };

        var args = new List<string>
            {
                "-9"
            };
        args.AddRange(toKill);

        await _processManager.ExecuteCommandAsync("killall", args, log, TimeSpan.FromSeconds(10));
        State = DeviceState.Shutdown;

        var dirsToBeDeleted = new[] {
                Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), "Library", "Saved Application State", "com.apple.watchsimulator.savedState"),
                Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), "Library", "Saved Application State", "com.apple.iphonesimulator.savedState"),
            };

        foreach (var dir in dirsToBeDeleted)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch (Exception e)
            {
                log.WriteLine("Could not delete the directory '{0}': {1}", dir, e.Message);
            }
        }
    }

    private async Task OpenSimulator(ILog log)
    {
        string simulator_app;

        if (IsWatchSimulator && _processManager.XcodeVersion.Major < 9)
        {
            simulator_app = Path.Combine(_processManager.XcodeRoot, "Contents", "Developer", "Applications", "Simulator (Watch).app");
        }
        else
        {
            simulator_app = Path.Combine(_processManager.XcodeRoot, "Contents", "Developer", "Applications", "Simulator.app");
            if (!Directory.Exists(simulator_app))
            {
                simulator_app = Path.Combine(_processManager.XcodeRoot, "Contents", "Developer", "Applications", "iOS Simulator.app");
            }
        }

        await _processManager.ExecuteCommandAsync("open", new[] { "-a", simulator_app, "--args", "-CurrentDeviceUDID", UDID }, log, TimeSpan.FromSeconds(15));
    }

    public async Task<bool> PrepareSimulator(ILog log, params string[] bundleIdentifiers)
    {
        // Kill all existing processes
        await KillEverything(log);

        // We shutdown and erase all simulators.
        await Erase(log);

        var tccDB = Path.Combine(DataPath, "data", "Library", "TCC", "TCC.db");
        if (!File.Exists(tccDB))
        {
            log.WriteLine("Booting the simulator to create TCC.db");
            await _processManager.ExecuteXcodeCommandAsync("simctl", new[] { "boot", UDID }, log, TimeSpan.FromMinutes(1));

            var tccCreationTimeout = 60;
            var watch = new Stopwatch();
            watch.Start();
            while (!File.Exists(tccDB) && watch.Elapsed.TotalSeconds < tccCreationTimeout)
            {
                log.WriteLine("Waiting for simulator to create TCC.db... {0}", (int)(tccCreationTimeout - watch.Elapsed.TotalSeconds));
                await Task.Delay(TimeSpan.FromSeconds(0.250));
            }
        }

        var result = true;
        if (File.Exists(tccDB))
        {
            log.WriteLine("TCC.db found for the simulator {0} (SimRuntime={1} and SimDeviceType={1})", UDID, SimRuntime, SimDeviceType);
            bundleIdentifiers = bundleIdentifiers.Where(id => !string.IsNullOrEmpty(id)).ToArray();
            if (bundleIdentifiers.Any())
            {
                log.WriteLine($"Storing adequate permissions in TCC.db to prevent dialog boxes in the test apps: {string.Join(", ", bundleIdentifiers)}", UDID);
                result &= await _tCCDatabase.AgreeToPromptsAsync(SimRuntime, tccDB, UDID, log, bundleIdentifiers);
            }
        }
        else
        {
            log.WriteLine("TCC.db not found for the simulator {0} (SimRuntime={1} and SimDeviceType={1})", UDID, SimRuntime, SimDeviceType);
        }

        // Make sure we're in a clean state
        await KillEverything(log);

        // Make 100% sure we're shutdown
        await Shutdown(log);

        return result;
    }

    public async Task<bool> Boot(ILog log, CancellationToken cancellationToken)
    {
        if (State == DeviceState.Booted)
        {
            log.WriteLine($"Simulator '{Name}' is already booted");
            return true;
        }

        log.WriteLine($"Booting simulator '{Name}'");

        var args = new MlaunchArguments
        {
            new SimulatorUDIDArgument(this),
            new LaunchSimulatorArgument(),
        };

        var watch = Stopwatch.StartNew();

        var result = await _processManager.ExecuteCommandAsync(
            args,
            log,
            TimeSpan.FromSeconds(30),
            verbosity: 2,
            cancellationToken: cancellationToken);

        if (!result.Succeeded)
        {
            log.WriteLine($"Failed to boot the simulator '{Name}'");
            return false;
        }

        log.WriteLine($"Simulator '{Name}' booted in {(int)watch.Elapsed.TotalSeconds} seconds");
        State = DeviceState.Booted;

        return true;
    }

    public async Task<string> GetAppBundlePath(ILog log, string bundleIdentifier, CancellationToken cancellationToken)
    {
        log.WriteLine($"Querying '{Name}' for bundle path of '{bundleIdentifier}'..");

        var output = new MemoryLog() { Timestamp = false };
        var result = await _processManager.ExecuteXcodeCommandAsync(
            "simctl",
            new[] { "get_app_container", UDID, bundleIdentifier },
            log,
            output,
            output,
            TimeSpan.FromSeconds(30));

        if (!result.Succeeded)
        {
            throw new Exception($"Failed to get information for '{bundleIdentifier}'. Please check the app is installed");
        }

        var bundlePath = output.ToString().Trim();
        log.WriteLine($"Found installed app bundle at '{bundlePath}'");

        return bundlePath;
    }
}
