// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;

internal class NoQuitArgument : SwitchArgument
{
    public NoQuitArgument()
        : base("no-quit", "Don't quit the xharness process after the tests are done running. Implies --no-headless", false)
    {
    }
}
