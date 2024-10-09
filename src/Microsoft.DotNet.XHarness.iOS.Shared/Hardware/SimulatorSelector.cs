// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

public interface ISimulatorSelector
{
    string GetRuntimePrefix(TestTargetOs target);
    string GetDeviceType(TestTargetOs target, bool minVersion);
    void GetCompanionRuntimeAndDeviceType(TestTargetOs target, bool minVersion, out string? companionRuntime, out string? companionDeviceType);
    ISimulatorDevice SelectSimulator(IEnumerable<ISimulatorDevice> simulators);
}

public class DefaultSimulatorSelector : ISimulatorSelector
{
    public virtual string GetRuntimePrefix(TestTargetOs target)
    {
        return target.Platform switch
        {
            TestTarget.Simulator_iOS64 => "com.apple.CoreSimulator.SimRuntime.iOS-",
            TestTarget.Simulator_tvOS => "com.apple.CoreSimulator.SimRuntime.tvOS-",
            TestTarget.Simulator_watchOS => "com.apple.CoreSimulator.SimRuntime.watchOS-",
            TestTarget.Simulator_xrOS => "com.apple.CoreSimulator.SimRuntime.xrOS-",
            _ => throw new Exception(string.Format("Invalid simulator target: {0}", target))
        };
    }

    public virtual string GetDeviceType(TestTargetOs target, bool minVersion)
    {
        return target.Platform switch
        {
            TestTarget.Simulator_iOS64 => "com.apple.CoreSimulator.SimDeviceType." + (minVersion ? "iPhone-6s" : "iPhone-XS"),
            TestTarget.Simulator_tvOS => "com.apple.CoreSimulator.SimDeviceType.Apple-TV-1080p",
            TestTarget.Simulator_watchOS => "com.apple.CoreSimulator.SimDeviceType." + (minVersion ? "Apple-Watch-38mm" : "Apple-Watch-Series-3-38mm"),
            TestTarget.Simulator_xrOS => "com.apple.CoreSimulator.SimDeviceType.Apple-Vision-Pro",
            _ => throw new Exception(string.Format("Invalid simulator target: {0}", target))
        };
    }

    public virtual void GetCompanionRuntimeAndDeviceType(TestTargetOs target, bool minVersion, out string? companionRuntime, out string? companionDeviceType)
    {
        if (target.Platform == TestTarget.Simulator_watchOS)
        {
            companionRuntime = "com.apple.CoreSimulator.SimRuntime.iOS-" + (minVersion ? SdkVersions.MinWatchOSCompanionSimulator : SdkVersions.MaxWatchOSCompanionSimulator).Replace('.', '-');
            companionDeviceType = "com.apple.CoreSimulator.SimDeviceType." + (minVersion ? "iPhone-6s" : "iPhone-XS");
        }
        else
        {
            companionRuntime = null;
            companionDeviceType = null;
        }
    }

    public ISimulatorDevice SelectSimulator(IEnumerable<ISimulatorDevice> simulators)
    {
        // Put Booted/Booting in front of Shutdown/Unknown
        return simulators.OrderByDescending(s => s.State).First();
    }
}
