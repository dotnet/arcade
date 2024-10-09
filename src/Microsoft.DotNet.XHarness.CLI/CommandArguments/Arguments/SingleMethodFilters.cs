// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments;

/// <summary>
/// Methods to be included in the test run while all others are ignored.
/// </summary>
internal class SingleMethodFilters : RepeatableArgument
{
    public SingleMethodFilters()
        : base("method|m=",
              "Method to be ran in the test application. When this parameter is used only the " +
              "tests that have been provided by the '--method' and '--class' arguments will be ran. All other test will be ignored")
    {
    }
}
