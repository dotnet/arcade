// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared;

public enum TestTarget
{
    None,

    Simulator_iOS64,
    Simulator_tvOS,
    Simulator_watchOS,
    Simulator_xrOS,

    Device_iOS,
    Device_tvOS,
    Device_watchOS,
    Device_xrOS,

    MacCatalyst,
}

public class TestTargetOs
{
    public static readonly TestTargetOs None = new(TestTarget.None, null);

    /// <summary>
    /// Platform, i.e. Simulator iOS x64
    /// </summary>
    public TestTarget Platform { get; }

    /// <summary>
    /// OS version, i.e. "13.4".
    /// </summary>
    public string? OSVersion { get; }

    public TestTargetOs(TestTarget platform, string? osVersion)
    {
        Platform = platform;
        OSVersion = osVersion;
    }
}

public static class TestTargetExtensions
{
    public static RunMode ToRunMode(this TestTarget target) => target switch
    {
        TestTarget.Simulator_iOS64 => RunMode.iOS,
        TestTarget.Simulator_tvOS => RunMode.TvOS,
        TestTarget.Simulator_watchOS => RunMode.WatchOS,
        TestTarget.Simulator_xrOS => RunMode.xrOS,

        TestTarget.Device_iOS => RunMode.iOS,
        TestTarget.Device_tvOS => RunMode.TvOS,
        TestTarget.Device_watchOS => RunMode.WatchOS,
        TestTarget.Device_xrOS => RunMode.xrOS,

        TestTarget.MacCatalyst => RunMode.MacOS,

        _ => throw new ArgumentOutOfRangeException($"Unknown target: {target}"),
    };

    public static bool IsSimulator(this TestTarget target) => target switch
    {
        TestTarget.Simulator_iOS64 => true,
        TestTarget.Simulator_tvOS => true,
        TestTarget.Simulator_watchOS => true,
        TestTarget.Simulator_xrOS => true,

        TestTarget.Device_iOS => false,
        TestTarget.Device_tvOS => false,
        TestTarget.Device_watchOS => false,
        TestTarget.Device_xrOS => false,

        TestTarget.MacCatalyst => true,

        _ => throw new ArgumentOutOfRangeException($"Unknown target: {target}"),
    };

    public static bool IsWatchOSTarget(this TestTarget target) => target switch
    {
        TestTarget.Simulator_watchOS => true,
        TestTarget.Device_watchOS => true,
        _ => false,
    };
}
