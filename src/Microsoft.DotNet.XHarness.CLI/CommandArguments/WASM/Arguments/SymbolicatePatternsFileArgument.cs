// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;

internal class SymbolicatePatternsFileArgument : StringArgument
{
    private const string HelpMessage =
                            @"File containing .net regex patterns for replacing wasm-function numbers with the corresponding names." +
                            @" The regex must contain a group named `funcNum` for getting the function number." +
                            @" And an optional group named `replaceSection` for matching the part of the string to be replaced by the name.";
    public SymbolicatePatternsFileArgument()
        : base("symbol-patterns=", HelpMessage)
    {
    }

    public override void Validate()
    {
        base.Validate();

        if (Value != null && !File.Exists(Value))
        {
            throw new ArgumentException($"Cannot find error patterns file {Value}");
        }
    }
}
