// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

/// <summary>
/// Name of a specific device we want to target.
/// </summary>
internal class DeviceNameArgument : StringArgument
{
    public DeviceNameArgument() : base("device=", "Name or UDID of a simulator/device you wish to target")
    {
    }
}
