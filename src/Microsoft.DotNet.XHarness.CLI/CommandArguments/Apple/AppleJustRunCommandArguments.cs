// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.iOS.Shared;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

internal class AppleJustRunCommandArguments : XHarnessCommandArguments, IAppleAppRunArguments
{
    public BundleIdentifierArgument BundleIdentifier { get; } = new();
    public TargetArgument Target { get; } = new();
    public OutputDirectoryArgument OutputDirectory { get; } = new();
    public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));
    public XcodeArgument XcodeRoot { get; } = new();
    public MlaunchArgument MlaunchPath { get; } = new();
    public DeviceNameArgument DeviceName { get; } = new();
    public IncludeWirelessArgument IncludeWireless { get; } = new();
    public EnableLldbArgument EnableLldb { get; } = new();
    public EnvironmentalVariablesArgument EnvironmentalVariables { get; } = new();
    public ExpectedExitCodeArgument ExpectedExitCode { get; } = new((int)ExitCode.SUCCESS);
    public SignalAppEndArgument SignalAppEnd { get; } = new();
    public NoWaitArgument NoWait { get; } = new();

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
        BundleIdentifier,
        Target,
        OutputDirectory,
        DeviceName,
        IncludeWireless,
        Timeout,
        ExpectedExitCode,
        XcodeRoot,
        MlaunchPath,
        EnableLldb,
        SignalAppEnd,
        NoWait,
        EnvironmentalVariables,
    };

    public override void Validate()
    {
        base.Validate();

        if (Target.Value.Platform == TestTarget.MacCatalyst)
        {
            throw new ArgumentException("This command is not supported with the maccatalyst target");
        }

        if (SignalAppEnd && NoWait)
        {
            throw new ArgumentException("--signal-app-end cannot be used in combination with --no-wait");
        }
    }
}
