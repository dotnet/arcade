// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments;

/// <summary>
/// Tests classes to be included in the run while all others are ignored.
/// </summary>
internal class ClassMethodFilters : RepeatableArgument
{
    public ClassMethodFilters()
        : base("class|c=",
              "Test class to be ran in the test application. When this parameter is used only the " +
              "tests that have been provided by the '--method' and '--class' arguments will be ran. All other test will be ignored")
    {
    }
}
