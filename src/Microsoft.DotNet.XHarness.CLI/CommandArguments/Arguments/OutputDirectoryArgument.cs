// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments;

/// <summary>
/// Path where the outputs of execution will be stored
/// </summary>
internal class OutputDirectoryArgument : RequiredPathArgument
{
    public OutputDirectoryArgument() : base("output-directory=|o=", "Directory where logs and results will be saved")
    {
    }

    public override void Validate()
    {
        if (!Directory.Exists(Value ?? throw new ArgumentNullException("You must provide an output directory where results will be stored")))
        {
            Directory.CreateDirectory(Value);
        }
    }
}
