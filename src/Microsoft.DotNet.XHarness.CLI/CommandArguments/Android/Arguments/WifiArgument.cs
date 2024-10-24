// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

/// <summary>
/// Switch on/off wifi on the device.
/// </summary>
internal class WifiArgument : Argument<WifiStatus>
{
    public WifiArgument()
        : base("wifi:", "Enable/disable WiFi. WiFi state is ignored by default. If passed without value, 'enable' is assumed", WifiStatus.Unknown)
    {
    }

    public override void Action(string argumentValue)
    {
        Value = string.IsNullOrEmpty(argumentValue)
            ? WifiStatus.Enable
            : ParseArgument("wifi", argumentValue, invalidValues: WifiStatus.Unknown);
    }
}

internal enum WifiStatus
{
    /// <summary>
    /// Not checked by default.
    /// </summary>
    Unknown,
    Enable,
    Disable,
}
