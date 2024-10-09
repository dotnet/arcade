// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

public interface ISimulatorLoader : IDeviceLoader
{
    IEnumerable<SimRuntime> SupportedRuntimes { get; }
    IEnumerable<SimDeviceType> SupportedDeviceTypes { get; }
    IEnumerable<SimulatorDevice> AvailableDevices { get; }
    IEnumerable<SimDevicePair> AvailableDevicePairs { get; }

    Task<ISimulatorDevice> FindCompanionDevice(ILog log, ISimulatorDevice device, CancellationToken cancellationToken = default);
    IEnumerable<ISimulatorDevice?> SelectDevices(TestTarget target, ILog log, bool minVersion, CancellationToken cancellationToken = default);
    IEnumerable<ISimulatorDevice?> SelectDevices(TestTargetOs target, ILog log, bool minVersion, CancellationToken cancellationToken = default);

    Task<(ISimulatorDevice Simulator, ISimulatorDevice? CompanionSimulator)> FindSimulators(
        TestTarget target,
        ILog log,
        bool createIfNeeded = true,
        bool minVersion = false,
        CancellationToken cancellationToken = default);

    Task<(ISimulatorDevice Simulator, ISimulatorDevice? CompanionSimulator)> FindSimulators(
        TestTargetOs target,
        ILog log,
        bool createIfNeeded = true,
        bool minVersion = false,
        CancellationToken cancellationToken = default);

    Task<(ISimulatorDevice Simulator, ISimulatorDevice? CompanionSimulator)> FindSimulators(
        TestTargetOs target,
        ILog log,
        int retryCount,
        bool createIfNeeded = true,
        bool minVersion = false,
        CancellationToken cancellationToken = default);
}
