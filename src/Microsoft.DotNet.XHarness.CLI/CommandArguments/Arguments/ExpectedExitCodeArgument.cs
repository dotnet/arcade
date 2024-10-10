// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments;

/// <summary>
/// Exit code returned by the instrumentation for a successful run.
/// </summary>
internal class ExpectedExitCodeArgument : IntArgument
{
    public ExpectedExitCodeArgument(int defaultValue)
        : base("expected-exit-code=", $"If specified, sets the expected exit code for a successful run. Default set to {defaultValue}", defaultValue)
    {
    }
}
