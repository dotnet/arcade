// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments;

public class DiagnosticsArgument : StringArgument
{
    public DiagnosticsArgument() : base("diagnostics=", "Path to a file where diagnostics data from the run will be stored")
    {
    }
}
