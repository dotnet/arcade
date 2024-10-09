// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments;

internal class UseJsonArgument : SwitchArgument
{
    public UseJsonArgument()
        : base("json", "Use json as the output format", false)
    {
    }
}
