// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;

internal class RuntimePathArgument : RequiredStringArgument
{
    public RuntimePathArgument()
        : base("runtime-folder=|r=", "Path to the shared runtime")
    {
    }
}
