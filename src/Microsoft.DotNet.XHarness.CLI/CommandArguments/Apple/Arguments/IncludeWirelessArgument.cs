// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

/// <summary>
/// If enabled, takes longer to list devices and looks for wirelessly connected ones too.
/// </summary>
internal class IncludeWirelessArgument : SwitchArgument
{
    public IncludeWirelessArgument() : base("wireless:|include-wireless-devices:", "Also look for wirelessly connected devices (takes longer)", false)
    {
    }
}
