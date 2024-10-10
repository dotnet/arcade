// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;

internal class BrowserArgument : Argument<Browser>
{
    public BrowserArgument()
        : base("browser=|b=", "Specifies the browser to be used. Default is Chrome", Browser.Chrome)
    {
    }

    public override void Action(string argumentValue) =>
        Value = ParseArgument<Browser>("browser", argumentValue);

    public override void Validate()
    {
        base.Validate();

        if (Value == Browser.Safari && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            throw new ArgumentException("Safari is only supported on OSX");
        }
    }
}

/// <summary>
/// Specifies the name of a Browser used to run the WASM application
/// </summary>
internal enum Browser
{
    /// <summary>
    /// Chrome
    /// </summary>
    Chrome,

    /// <summary>
    /// Safari
    /// </summary>
    Safari,

    /// <summary>
    /// Firefox
    /// </summary>
    Firefox,

    /// <summary>
    /// Edge
    /// </summary>
    Edge
}
