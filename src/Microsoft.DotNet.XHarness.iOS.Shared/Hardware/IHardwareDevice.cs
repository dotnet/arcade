// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

public interface IHardwareDevice : IDevice
{
    string DeviceIdentifier { get; }
    DeviceClass DeviceClass { get; }
    string? CompanionIdentifier { get; }
    string? BuildVersion { get; }
    string? ProductVersion { get; }
    string? ProductType { get; }
    string InterfaceType { get; }
    bool? IsUsableForDebugging { get; }
    bool IsLocked { get; }
    bool IsPaired { get; }
    int DebugSpeed { get; }
    DevicePlatform DevicePlatform { get; }
    bool Supports64Bit { get; }
    bool Supports32Bit { get; }
    Architecture Architecture { get; }
}
