// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.Common.CLI;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

internal class AppleRunCommandArguments : XHarnessCommandArguments, IAppleAppRunArguments
{
    public AppPathArgument AppBundlePath { get; } = new();
    public TargetArgument Target { get; } = new();
    public OutputDirectoryArgument OutputDirectory { get; } = new();
    public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));
    public LaunchTimeoutArgument LaunchTimeout { get; } = new(TimeSpan.FromMinutes(5));
    public XcodeArgument XcodeRoot { get; } = new();
    public MlaunchArgument MlaunchPath { get; } = new();
    public DeviceNameArgument DeviceName { get; } = new();
    public IncludeWirelessArgument IncludeWireless { get; } = new();
    public EnableLldbArgument EnableLldb { get; } = new();
    public EnvironmentalVariablesArgument EnvironmentalVariables { get; } = new();
    public ResetSimulatorArgument ResetSimulator { get; } = new();
    public ExpectedExitCodeArgument ExpectedExitCode { get; } = new((int)ExitCode.SUCCESS);
    public SignalAppEndArgument SignalAppEnd { get; } = new();
    public NoWaitArgument NoWait { get; } = new();

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
        AppBundlePath,
        Target,
        OutputDirectory,
        DeviceName,
        IncludeWireless,
        Timeout,
        LaunchTimeout,
        ExpectedExitCode,
        XcodeRoot,
        MlaunchPath,
        EnableLldb,
        SignalAppEnd,
        NoWait,
        ResetSimulator,
        EnvironmentalVariables,
    };

    public override void Validate()
    {
        base.Validate();

        if (SignalAppEnd && NoWait)
        {
            throw new ArgumentException("--signal-app-end cannot be used in combination with --no-wait");
        }
    }
}
