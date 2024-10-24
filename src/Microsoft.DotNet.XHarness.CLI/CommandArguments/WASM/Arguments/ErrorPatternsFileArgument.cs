// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;

internal class ErrorPatternsFileArgument : StringArgument
{
    public ErrorPatternsFileArgument()
        : base("error-patterns=|p=", "File containing error patterns. Each line prefixed with '@', or '%' for a simple string, or a .net regex, respectively")
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
