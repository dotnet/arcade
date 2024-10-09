// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Collections;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

public class SimulatorLoader : ISimulatorLoader
{
    private readonly BlockingEnumerableCollection<SimRuntime> _supportedRuntimes = new();
    private readonly BlockingEnumerableCollection<SimDeviceType> _supportedDeviceTypes = new();
    private readonly BlockingEnumerableCollection<SimulatorDevice> _availableDevices = new();
    private readonly BlockingEnumerableCollection<SimDevicePair> _availableDevicePairs = new();

    private readonly SemaphoreSlim _semaphore = new(1);
    private readonly IMlaunchProcessManager _processManager;
    private readonly ISimulatorSelector _simulatorSelector;
    private bool _loaded;

    public IEnumerable<SimRuntime> SupportedRuntimes => _supportedRuntimes;
    public IEnumerable<SimDeviceType> SupportedDeviceTypes => _supportedDeviceTypes;
    public IEnumerable<SimulatorDevice> AvailableDevices => _availableDevices;
    public IEnumerable<SimDevicePair> AvailableDevicePairs => _availableDevicePairs;

    public SimulatorLoader(IMlaunchProcessManager processManager, ISimulatorSelector? simulatorSelector = null)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _simulatorSelector = simulatorSelector ?? new DefaultSimulatorSelector();
    }

    public async Task LoadDevices(
        ILog log,
        bool includeLocked = false,
        bool forceRefresh = false,
        bool listExtraData = false,
        bool includeWirelessDevices = true,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (_loaded)
        {
            if (!forceRefresh)
            {
                _semaphore.Release();
                return;
            }

            _supportedRuntimes.Reset();
            _supportedDeviceTypes.Reset();
            _availableDevices.Reset();
            _availableDevicePairs.Reset();
        }

        var tmpfile = Path.GetTempFileName();

        try
        {
            var arguments = new MlaunchArguments(
                new ListSimulatorsArgument(tmpfile),
                new XmlOutputFormatArgument());

            var result = await _processManager.ExecuteCommandAsync(arguments, log, timeout: TimeSpan.FromMinutes(6), cancellationToken: cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (!result.Succeeded)
            {
                // mlaunch can sometimes return 0 but hang and timeout. It still outputs returns valid content to the tmp file
                log.WriteLine($"mlaunch failed when listing simulators but trying to parse the results anyway");
            }

            var fileInfo = new FileInfo(tmpfile);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                throw new Exception($"Failed to list simulators - no XML with devices found. " +
                    $"mlaunch {(result.TimedOut ? "timed out" : "exited")} with {result.ExitCode})");
            }

            log.WriteLine($"Simulator listing finished ({Math.Ceiling(((double)fileInfo.Length) / 1024)} kB)");

            var simulatorData = new XmlDocument();
            simulatorData.LoadWithoutNetworkAccess(tmpfile);
            foreach (XmlNode? sim in simulatorData.SelectNodes("/MTouch/Simulator/SupportedRuntimes/SimRuntime"))
            {
                if (sim == null)
                {
                    continue;
                }

                _supportedRuntimes.Add(new SimRuntime(
                    name: sim.SelectSingleNode("Name").InnerText,
                    identifier: sim.SelectSingleNode("Identifier").InnerText,
                    version: long.Parse(sim.SelectSingleNode("Version").InnerText)));
            }

            foreach (XmlNode? sim in simulatorData.SelectNodes("/MTouch/Simulator/SupportedDeviceTypes/SimDeviceType"))
            {
                if (sim == null)
                {
                    continue;
                }

                _supportedDeviceTypes.Add(new SimDeviceType(
                    name: sim.SelectSingleNode("Name").InnerText,
                    identifier: sim.SelectSingleNode("Identifier").InnerText,
                    productFamilyId: sim.SelectSingleNode("ProductFamilyId").InnerText,
                    minRuntimeVersion: long.Parse(sim.SelectSingleNode("MinRuntimeVersion").InnerText),
                    maxRuntimeVersion: long.Parse(sim.SelectSingleNode("MaxRuntimeVersion").InnerText),
                    supports64Bits: bool.Parse(sim.SelectSingleNode("Supports64Bits").InnerText)));
            }

            foreach (XmlNode? sim in simulatorData.SelectNodes("/MTouch/Simulator/AvailableDevices/SimDevice"))
            {
                if (sim == null)
                {
                    continue;
                }

                _availableDevices.Add(new SimulatorDevice(_processManager, new TCCDatabase(_processManager))
                {
                    Name = sim.Attributes["Name"].Value,
                    UDID = sim.Attributes["UDID"].Value,
                    State = sim.Attributes["State"]?.Value switch
                    {
                        "Booting" => DeviceState.Booting,
                        "Booted" => DeviceState.Booted,
                        "ShuttingDown" => DeviceState.ShuttingDown,
                        "Shutdown" => DeviceState.Shutdown,
                        _ => DeviceState.Unknown,
                    },
                    SimRuntime = sim.SelectSingleNode("SimRuntime").InnerText,
                    SimDeviceType = sim.SelectSingleNode("SimDeviceType").InnerText,
                    DataPath = sim.SelectSingleNode("DataPath").InnerText,
                    LogPath = sim.SelectSingleNode("LogPath").InnerText,
                });
            }

            var sim_device_pairs = simulatorData.
                SelectNodes("/MTouch/Simulator/AvailableDevicePairs/SimDevicePair").
                Cast<XmlNode>().
                // There can be duplicates, so remove those.
                Distinct(new SimulatorXmlNodeComparer());

            foreach (XmlNode sim in sim_device_pairs)
            {
                _availableDevicePairs.Add(new SimDevicePair(
                    UDID: sim.Attributes["UDID"].Value,
                    companion: sim.SelectSingleNode("Companion").InnerText,
                    gizmo: sim.SelectSingleNode("Gizmo").InnerText));
            }

            _loaded = true;
        }
        finally
        {
            _supportedRuntimes.SetCompleted();
            _supportedDeviceTypes.SetCompleted();
            _availableDevices.SetCompleted();
            _availableDevicePairs.SetCompleted();
            File.Delete(tmpfile);
            _semaphore.Release();
        }
    }

    private string CreateName(string deviceType, string runtime)
    {
        var runtimeName = _supportedRuntimes?.Where(v => v.Identifier == runtime).FirstOrDefault()?.Name ?? Path.GetExtension(runtime).Substring(1);
        var deviceName = _supportedDeviceTypes?.Where(v => v.Identifier == deviceType).FirstOrDefault()?.Name ?? Path.GetExtension(deviceType).Substring(1);
        return $"{deviceName} ({runtimeName}) - created by XHarness";
    }

    // Will return all devices that match the runtime + devicetype (even if a new device was created, any other devices will also be returned)
    private async Task<IEnumerable<ISimulatorDevice>> FindOrCreateDevicesAsync(
        ILog log,
        string runtime,
        string devicetype,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (runtime is null)
        {
            throw new ArgumentNullException(nameof(runtime));
        }

        if (devicetype is null)
        {
            throw new ArgumentNullException(nameof(devicetype));
        }

        IEnumerable<ISimulatorDevice> devices;

        if (!force)
        {
            if (!_loaded)
            {
                await LoadDevices(log, cancellationToken: cancellationToken);
            }

            devices = AvailableDevices.Where(v => v.SimRuntime == runtime && v.SimDeviceType == devicetype);
            if (devices.Any())
            {
                return devices;
            }
        }

        var args = new[] { "create", CreateName(devicetype, runtime), devicetype, runtime };
        var rv = await _processManager.ExecuteXcodeCommandAsync("simctl", args, log, TimeSpan.FromMinutes(1), cancellationToken);
        if (!rv.Succeeded)
        {
            var message = $"Could not create device{Environment.NewLine}" +
                $"runtime: {runtime}{Environment.NewLine}" +
                $"device type: {devicetype}";
            log.WriteLine(message);
            throw new NoDeviceFoundException(message);
        }

        await LoadDevices(log, forceRefresh: true, cancellationToken: cancellationToken);

        devices = AvailableDevices.Where((ISimulatorDevice v) => v.SimRuntime == runtime && v.SimDeviceType == devicetype);
        if (!devices.Any())
        {
            var message = $"Simulator not found after creating it{Environment.NewLine}" +
                $"runtime: {runtime}{Environment.NewLine}" +
                $"device type: {devicetype}";
            log.WriteLine(message);
            throw new NoDeviceFoundException(message);
        }

        return devices;
    }

    private async Task<bool> CreateDevicePair(
        ILog log,
        ISimulatorDevice device,
        ISimulatorDevice companion_device,
        string runtime,
        string devicetype,
        bool createDevice,
        CancellationToken cancellationToken = default)
    {
        if (createDevice)
        {
            // watch device is already paired to some other phone. Create a new watch device
            var matchingDevices = await FindOrCreateDevicesAsync(log, runtime, devicetype, force: true, cancellationToken);
            var unPairedDevices = matchingDevices.Where(v => !AvailableDevicePairs.Any(p => p.Gizmo == v.UDID));
            if (device != null)
            {
                // If we're creating a new watch device, assume that the one we were given is not usable.
                unPairedDevices = unPairedDevices.Where(v => v.UDID != device.UDID);
            }

            if (unPairedDevices?.Any() != true)
            {
                return false;
            }

            device = unPairedDevices.First();
        }

        log.WriteLine($"Creating device pair for '{device.Name}' and '{companion_device.Name}'");

        var capturedLog = new StringBuilder();
        var pairLog = new CallbackLog((value) =>
        {
            log.Write(value);
            capturedLog.Append(value);
        });

        var args = new[] { "pair", device.UDID, companion_device.UDID };
        var rv = await _processManager.ExecuteXcodeCommandAsync("simctl", args, pairLog, TimeSpan.FromMinutes(1), cancellationToken);
        if (!rv.Succeeded)
        {
            if (!createDevice)
            {
                var try_creating_device = false;
                var captured_log = capturedLog.ToString();
                try_creating_device |= captured_log.Contains("At least one of the requested devices is already paired with the maximum number of supported devices and cannot accept another pairing.");
                try_creating_device |= captured_log.Contains("The selected devices are already paired with each other.");
                if (try_creating_device)
                {
                    log.WriteLine($"Could not create device pair for '{device.Name}' ({device.UDID}) and '{companion_device.Name}' ({companion_device.UDID}), but will create a new watch device and try again.");
                    return await CreateDevicePair(log, device, companion_device, runtime, devicetype, true);
                }
            }

            log.WriteLine($"Could not create device pair for '{device.Name}' ({device.UDID}) and '{companion_device.Name}' ({companion_device.UDID})");
            return false;
        }

        return true;
    }

    private async Task<SimDevicePair?> FindOrCreateDevicePairAsync(
        ILog log,
        IEnumerable<ISimulatorDevice> devices,
        IEnumerable<ISimulatorDevice> companionDevices,
        CancellationToken cancellationToken = default)
    {
        // Check if we already have a device pair with the specified devices
        var pairs = AvailableDevicePairs.Where(pair =>
        {
            if (!devices.Any(v => v.UDID == pair.Gizmo))
            {
                return false;
            }

            if (!companionDevices.Any(v => v.UDID == pair.Companion))
            {
                return false;
            }

            return true;
        });

        if (!pairs.Any())
        {
            // No device pair. Create one.
            // First check if the watch is already paired
            var unPairedDevices = devices.Where(v => !AvailableDevicePairs.Any(p => p.Gizmo == v.UDID));
            var unpairedDevice = unPairedDevices.FirstOrDefault();
            var companion_device = companionDevices.First();
            var device = devices.First();
            if (!await CreateDevicePair(
                    log,
                    unpairedDevice,
                    companion_device,
                    device.SimRuntime,
                    device.SimDeviceType,
                    unpairedDevice == null,
                    cancellationToken: cancellationToken))
            {
                return null;
            }

            await LoadDevices(log, forceRefresh: true, cancellationToken: cancellationToken);

            pairs = AvailableDevicePairs.Where((pair) =>
            {
                if (!devices.Any(v => v.UDID == pair.Gizmo))
                {
                    return false;
                }

                if (!companionDevices.Any(v => v.UDID == pair.Companion))
                {
                    return false;
                }

                return true;
            });
        }

        return pairs.FirstOrDefault();
    }

    /// <summary>
    /// This is a new implementation that respects also target OS version and if that one is specified, looks for that specific simulator.
    /// Old implementation of FindSimulators is kept intact because it is being used in Xamarin Mac/iOS.
    /// </summary>
    public async Task<(ISimulatorDevice Simulator, ISimulatorDevice? CompanionSimulator)> FindSimulators(
        TestTargetOs target,
        ILog log,
        bool createIfNeeded = true,
        bool minVersion = false,
        CancellationToken cancellationToken = default)
    {
        var runtimePrefix = _simulatorSelector.GetRuntimePrefix(target);

        var runtimeVersion = target.OSVersion;

        if (runtimeVersion == null)
        {
            if (!_loaded)
            {
                await LoadDevices(log, cancellationToken: cancellationToken);
            }

            string? firstOsVersion = _supportedRuntimes
                .Where(r => r.Identifier.StartsWith(runtimePrefix))
                .OrderByDescending(r => r.Identifier)
                .FirstOrDefault()?
                .Identifier
                .Substring(runtimePrefix.Length);

            runtimeVersion = firstOsVersion ?? throw new NoDeviceFoundException($"Failed to find a suitable OS runtime version for {target.AsString()}");
        }

        string simulatorRuntime = runtimePrefix + runtimeVersion.Replace('.', '-');
        string simulatorDeviceType = _simulatorSelector.GetDeviceType(target, minVersion);

        // TODO: Allow to specify companion runtime
        _simulatorSelector.GetCompanionRuntimeAndDeviceType(target, minVersion, out var companionRuntime, out var companionDeviceType);

        var devices = await FindOrCreateDevicesAsync(log, simulatorRuntime, simulatorDeviceType, cancellationToken: cancellationToken);
        IEnumerable<ISimulatorDevice>? companionDevices = null;

        if (companionRuntime != null && companionDeviceType != null)
        {
            companionDevices = await FindOrCreateDevicesAsync(log, companionRuntime, companionDeviceType, cancellationToken: cancellationToken);
        }

        if (devices?.Any() != true)
        {
            throw new Exception($"Could not find or create devices{Environment.NewLine}" +
                $"runtime: {simulatorRuntime}{Environment.NewLine}" +
                $"device type: {simulatorDeviceType}");
        }

        ISimulatorDevice? simulator = null;
        ISimulatorDevice? companionSimulator = null;

        if (companionRuntime == null)
        {
            simulator = _simulatorSelector.SelectSimulator(devices);
        }
        else
        {
            if (companionDevices?.Any() != true)
            {
                throw new Exception($"Could not find or create companion devices{Environment.NewLine}" +
                    $"runtime: {companionRuntime}{Environment.NewLine}" +
                    $"device type: {companionDeviceType}");
            }

            var pair = await FindOrCreateDevicePairAsync(log, devices, companionDevices);
            if (pair == null)
            {
                throw new Exception($"Could not find or create device pair{Environment.NewLine}" +
                    $"runtime: {companionRuntime}{Environment.NewLine}" +
                    $"device type: {companionDeviceType}");
            }

            simulator = devices.First(v => v.UDID == pair.Gizmo);
            companionSimulator = companionDevices.First(v => v.UDID == pair.Companion);
        }

        if (simulator == null)
        {
            throw new Exception($"Could not find simulator{Environment.NewLine}" +
                $"runtime: {simulatorRuntime}{Environment.NewLine}" +
                $"device type: {simulatorDeviceType}");
        }

        log.WriteLine("Found simulator: {0} {1}", simulator.Name, simulator.UDID);

        if (companionSimulator != null)
        {
            log.WriteLine("Found companion simulator: {0} {1}", companionSimulator.Name, companionSimulator.UDID);
        }

        return (simulator, companionSimulator);
    }

    public Task<(ISimulatorDevice Simulator, ISimulatorDevice? CompanionSimulator)> FindSimulators(
        TestTarget target,
        ILog log,
        bool createIfNeeded = true,
        bool minVersion = false,
        CancellationToken cancellationToken = default)
    {
        TestTargetOs testTarget = target switch
        {
            TestTarget.Simulator_iOS64 => new TestTargetOs(target, minVersion ? SdkVersions.MiniOSSimulator : SdkVersions.MaxiOSSimulator),
            TestTarget.Simulator_tvOS => new TestTargetOs(target, minVersion ? SdkVersions.MinTVOSSimulator : SdkVersions.MaxTVOSSimulator),
            TestTarget.Simulator_watchOS => new TestTargetOs(target, minVersion ? SdkVersions.MinWatchOSSimulator : SdkVersions.MaxWatchOSSimulator),
            TestTarget.Simulator_xrOS => new TestTargetOs(target, minVersion ? SdkVersions.MinxrOSSimulator : SdkVersions.MaxxrOSSimulator),
            _ => throw new Exception(string.Format("Invalid simulator target: {0}", target))
        };

        return FindSimulators(testTarget, log, createIfNeeded: createIfNeeded, minVersion: minVersion, cancellationToken: cancellationToken);
    }

    public async Task<ISimulatorDevice> FindCompanionDevice(ILog log, ISimulatorDevice device, CancellationToken cancellationToken = default)
    {
        await LoadDevices(log, forceRefresh: false, cancellationToken: cancellationToken);

        var pair = _availableDevicePairs.Where(v => v.Gizmo == device.UDID).Single();
        return _availableDevices.Single(v => v.UDID == pair.Companion);
    }

    public async Task<(ISimulatorDevice Simulator, ISimulatorDevice? CompanionSimulator)> FindSimulators(
        TestTargetOs target,
        ILog log,
        int retryCount,
        bool createIfNeeded = true,
        bool minVersion = false,
        CancellationToken cancellationToken = default)
    {
        if (retryCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(retryCount));
        }

        int attempt = 1;
        while (true)
        {
            try
            {
                return await FindSimulators(target, log, cancellationToken: cancellationToken);
            }
            catch (Exception e)
            {
                log.WriteLine($"Failed to find/create simulator (attempt {attempt}/{retryCount}):" + Environment.NewLine + e);

                if (attempt == retryCount)
                {
                    throw new NoDeviceFoundException("Failed to find/create suitable simulator");
                }
            }
            finally
            {
                attempt++;
            }
        }
    }

    public IEnumerable<ISimulatorDevice?> SelectDevices(TestTarget target, ILog log, bool minVersion, CancellationToken cancellationToken = default)
        => new SimulatorEnumerable(this, target, minVersion, log, cancellationToken);
    public IEnumerable<ISimulatorDevice?> SelectDevices(TestTargetOs target, ILog log, bool minVersion, CancellationToken cancellationToken = default)
        => new SimulatorEnumerable(this, target, minVersion, log, cancellationToken);

    private class SimulatorXmlNodeComparer : IEqualityComparer<XmlNode>
    {
        public bool Equals(XmlNode? a, XmlNode? b)
        {
            if (a == null)
            {
                return b == null;
            }

            if (b == null)
            {
                return a == null;
            }

            return a["Gizmo"].InnerText == b["Gizmo"].InnerText && a["Companion"].InnerText == b["Companion"].InnerText;
        }

        public int GetHashCode(XmlNode? node)
        {
            if (node == null)
            {
                return 0;
            }

            return node["Gizmo"].InnerText.GetHashCode() ^ node["Companion"].InnerText.GetHashCode();
        }
    }

    /// <summary>
    /// SimulatorLoader only finds 2 devices - the main simulator and an optional companion device.
    /// For backwards compatibility of this library with Xamarin Mac/iOS, we need to be able to enumerate them, hence this class.
    /// </summary>
    private class SimulatorEnumerable : IEnumerable<ISimulatorDevice>, IAsyncEnumerable
    {
        private readonly Lazy<Task<(ISimulatorDevice, ISimulatorDevice?)>> _findTask;
        private readonly string _toString;

        public SimulatorEnumerable(ISimulatorLoader simulators, TestTarget target, bool minVersion, ILog log, CancellationToken cancellationToken = default)
        {
            _findTask = new Lazy<Task<(ISimulatorDevice, ISimulatorDevice?)>>(
                () => simulators.FindSimulators(target, log, minVersion: minVersion, cancellationToken: cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication);
            _toString = $"Simulators for {target} (MinVersion: {minVersion})";
        }

        public SimulatorEnumerable(ISimulatorLoader simulators, TestTargetOs target, bool minVersion, ILog log, CancellationToken cancellationToken = default)
        {
            _findTask = new Lazy<Task<(ISimulatorDevice, ISimulatorDevice?)>>(
                () => simulators.FindSimulators(target, log, minVersion: minVersion, cancellationToken: cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication);
            _toString = $"Simulators for {target} (MinVersion: {minVersion})";
        }

        public override string ToString() => _toString;

        public IEnumerator<ISimulatorDevice> GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public Task ReadyTask => _findTask.Value;

        public Task<(ISimulatorDevice, ISimulatorDevice?)> Find() => _findTask.Value;

        private class Enumerator : IEnumerator<ISimulatorDevice>
        {
            private readonly Lazy<(ISimulatorDevice, ISimulatorDevice?)> _devices;

            public Enumerator(SimulatorEnumerable enumerable)
            {
                _devices = new Lazy<(ISimulatorDevice, ISimulatorDevice?)>(() => enumerable.Find().Result, LazyThreadSafetyMode.ExecutionAndPublication);
            }

            private bool? _moved;

            public ISimulatorDevice Current
            {
                get
                {
                    if (_moved == null)
                    {
                        throw new InvalidOperationException("Call MoveNext() first!");
                    }

                    return _moved.Value ? _devices.Value.Item2! : _devices.Value.Item1;
                }
            }

            object IEnumerator.Current => Current ?? throw new NullReferenceException("Simulator device not found");

            public bool MoveNext()
            {
                if (_moved == null)
                {
                    _moved = false;
                    return _devices.Value.Item1 != null;
                }

                if (_moved.Value)
                {
                    return false;
                }

                _moved = true;
                return _devices.Value.Item2 != null;
            }

            public void Reset() => _moved = false;

            public void Dispose()
            {
            }
        }
    }
}
