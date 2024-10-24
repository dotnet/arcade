// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

/// <summary>
/// Kills running simulator processes and removes any previous data before running.
/// </summary>
internal class ResetSimulatorArgument : SwitchArgument
{
    public ResetSimulatorArgument()
        : base("reset-simulator", "Shuts down the simulator and clears all data before running. Shuts it down after the run too", false)
    {
    }
}
