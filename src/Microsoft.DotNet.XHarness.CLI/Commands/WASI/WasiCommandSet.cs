// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Mono.Options;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Wasi;

// Main WASI command set that contains the platform specific commands. This allows the command line to
// support different options in different platforms.
// Whenever the behavior does match, the goal is to have the same arguments for all platforms
public class WasiCommandSet : CommandSet
{
    public WasiCommandSet() : base("wasi")
    {
        Add(new WasiTestCommand());
    }
}
