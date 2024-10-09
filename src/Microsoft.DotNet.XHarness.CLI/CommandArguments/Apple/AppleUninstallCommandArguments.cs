// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.iOS.Shared;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

internal class AppleUninstallCommandArguments : XHarnessCommandArguments, IAppleAppRunArguments
{
    public BundleIdentifierArgument BundleIdentifier { get; } = new();
    public TargetArgument Target { get; } = new();
    public OutputDirectoryArgument OutputDirectory { get; } = new();
    public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(10));
    public XcodeArgument XcodeRoot { get; } = new();
    public MlaunchArgument MlaunchPath { get; } = new();
    public DeviceNameArgument DeviceName { get; } = new();
    public IncludeWirelessArgument IncludeWireless { get; } = new();

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
        BundleIdentifier,
        Target,
        OutputDirectory,
        Timeout,
        XcodeRoot,
        MlaunchPath,
        DeviceName,
        IncludeWireless,
    };

    public override void Validate()
    {
        base.Validate();

        if (Target.Value.Platform == TestTarget.MacCatalyst)
        {
            throw new ArgumentException("This command is not supported with the maccatalyst target");
        }
    }
}
