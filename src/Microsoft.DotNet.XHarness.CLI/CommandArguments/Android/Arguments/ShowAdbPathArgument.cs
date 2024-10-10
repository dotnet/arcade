// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

internal class ShowAdbPathArgument : SwitchArgument
{
    public ShowAdbPathArgument() : base("adb|show-adb-path", "Prints ONLY path to the adb executable XHarness is using", false)
    {
    }
}
