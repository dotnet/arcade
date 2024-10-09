// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

/// <summary>
/// If specified, attempt to run APK on that device.
/// If there is more than one device with required architecture, failing to specify this may cause execution failure.
/// </summary>
internal class DeviceIdArgument : StringArgument
{
    public DeviceIdArgument()
        : base("device-id=", "Device where APK should be installed")
    {
    }
}
