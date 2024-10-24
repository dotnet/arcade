// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

internal class AppleDeviceCommandArguments : XHarnessCommandArguments, IAppleArguments
{
    public DeviceNameArgument DeviceName { get; } = new();
    public IncludeWirelessArgument IncludeWireless { get; } = new();
    public XcodeArgument XcodeRoot { get; } = new();
    public MlaunchArgument MlaunchPath { get; } = new();

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
        DeviceName,
        IncludeWireless,
        XcodeRoot,
        MlaunchPath,
    };
}
