// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

internal class ShowDevicesUUIDArgument : SwitchArgument
{
    public ShowDevicesUUIDArgument()
        : base("include-devices-uuid", "Include the devices UUID. Defaults to true", true)
    {
    }
}
