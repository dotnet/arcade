// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;

namespace Microsoft.DotNet.XHarness.CLI.Tests;

internal class UnitTestArguments<TArgument> : XHarnessCommandArguments where TArgument : Argument
{
    public UnitTestArguments(TArgument argument)
    {
        Argument = argument;
    }

    public TArgument Argument { get; }

    protected override IEnumerable<Argument> GetArguments() => new[] { Argument };
}
