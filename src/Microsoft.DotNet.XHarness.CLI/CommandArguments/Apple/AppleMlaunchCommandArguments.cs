// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;

internal class AppleMlaunchCommandArguments : XHarnessCommandArguments, IAppleArguments
{
    public MlaunchArgument MlaunchPath { get; } = new();
    public XcodeArgument XcodeRoot { get; } = new();
    public TimeoutArgument Timeout { get; set; } = new(TimeSpan.FromMinutes(2));
    public EnvironmentalVariablesArgument EnvironmentalVariables { get; } = new();

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
        MlaunchPath,
        XcodeRoot,
        Timeout,
        EnvironmentalVariables,
    };
}
