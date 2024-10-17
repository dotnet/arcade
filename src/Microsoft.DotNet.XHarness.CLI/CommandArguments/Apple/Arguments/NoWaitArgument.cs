// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

internal class NoWaitArgument : SwitchArgument
{
    public NoWaitArgument() : base("no-wait|nowait", "Don't wait for the app to shut down", false)
    {
    }
}
