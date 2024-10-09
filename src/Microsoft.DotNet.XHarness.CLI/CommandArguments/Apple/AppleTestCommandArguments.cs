// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

internal class AppleTestCommandArguments : XHarnessCommandArguments, IAppleAppRunArguments
{
    public AppPathArgument AppBundlePath { get; } = new();
    public TargetArgument Target { get; } = new();
    public OutputDirectoryArgument OutputDirectory { get; } = new();
    public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));
    public LaunchTimeoutArgument LaunchTimeout { get; set; } = new(TimeSpan.FromMinutes(5));
    public XcodeArgument XcodeRoot { get; } = new();
    public MlaunchArgument MlaunchPath { get; } = new();
    public DeviceNameArgument DeviceName { get; } = new();
    public IncludeWirelessArgument IncludeWireless { get; } = new();
    public CommunicationChannelArgument CommunicationChannel { get; set; } = new();
    public XmlResultJargonArgument XmlResultJargon { get; set; } = new();
    public SingleMethodFilters SingleMethodFilters { get; } = new();
    public ClassMethodFilters ClassMethodFilters { get; } = new();
    public EnableLldbArgument EnableLldb { get; } = new();
    public EnvironmentalVariablesArgument EnvironmentalVariables { get; } = new();
    public ResetSimulatorArgument ResetSimulator { get; } = new();
    public SignalAppEndArgument SignalAppEnd { get; } = new();

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
        AppBundlePath,
        Target,
        OutputDirectory,
        Timeout,
        LaunchTimeout,
        XcodeRoot,
        MlaunchPath,
        DeviceName,
        IncludeWireless,
        CommunicationChannel,
        XmlResultJargon,
        SingleMethodFilters,
        ClassMethodFilters,
        EnableLldb,
        SignalAppEnd,
        EnvironmentalVariables,
        ResetSimulator,
    };
}
