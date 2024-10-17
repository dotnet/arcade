// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

/// <summary>
/// Path to where Xcode is located.
/// </summary>
internal class XcodeArgument : PathArgument
{
    public XcodeArgument() : base("xcode=", "Path where Xcode is installed. If not provided, determined from xcode-select", false)
    {
    }

    public override void Validate()
    {
        if (Value != null && !Directory.Exists(Value))
        {
            throw new ArgumentException($"Failed to find Xcode root at {Value}");
        }
    }
}
