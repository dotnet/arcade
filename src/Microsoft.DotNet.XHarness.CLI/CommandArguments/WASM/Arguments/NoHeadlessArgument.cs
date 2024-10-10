// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;

internal class NoHeadlessArgument : SwitchArgument
{
    public NoHeadlessArgument()
        : base("no-headless", "Don't run in headless mode", false)
    {
    }

    public void Set(bool value) => Value = value;
}
