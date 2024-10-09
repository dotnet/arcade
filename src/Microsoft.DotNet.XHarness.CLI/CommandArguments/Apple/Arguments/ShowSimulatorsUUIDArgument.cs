// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

internal class ShowSimulatorsUUIDArgument : SwitchArgument
{
    public ShowSimulatorsUUIDArgument()
        : base("include-simulator-uuid", "Include the simulators UUID. Defaults to false", false)
    {
    }
}
