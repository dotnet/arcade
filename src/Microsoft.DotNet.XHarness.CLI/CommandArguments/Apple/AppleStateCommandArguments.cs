// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

internal class AppleStateCommandArguments : XHarnessCommandArguments
{
    public XcodeArgument XcodeRoot { get; } = new();
    public MlaunchArgument MlaunchPath { get; set; } = new();
    public ShowSimulatorsUUIDArgument ShowSimulatorsUUID { get; set; } = new();
    public ShowDevicesUUIDArgument ShowDevicesUUID { get; set; } = new();
    public IncludeWirelessArgument IncludeWireless { get; } = new();
    public UseJsonArgument UseJson { get; set; } = new();

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
            XcodeRoot,
            MlaunchPath,
            ShowSimulatorsUUID,
            ShowDevicesUUID,
            IncludeWireless,
            UseJson,
    };
}
