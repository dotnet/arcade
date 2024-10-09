// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;

internal class HTMLFileArgument : RequiredStringArgument
{
    public HTMLFileArgument(string defaultValue)
        : base("html-file=", $"Main html file to load from the app directory. Default is {defaultValue}", defaultValue)
    {
    }

    public override void Validate()
    {
        base.Validate();

        if (Path.IsPathRooted(Value))
        {
            throw new ArgumentException("--html-file argument must be a relative path");
        }
    }
}
