// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;

internal class AndroidHeadlessInstallCommandArguments : XHarnessCommandArguments, IAndroidHeadlessAppRunArguments
{
    public TestPathArgument TestPath { get; } = new();
    public RuntimePathArgument RuntimePath { get; } = new();
    public TestAssemblyArgument TestAssembly { get; } = new();
    public TestScriptArgument TestScript { get; } = new();
    public OutputDirectoryArgument OutputDirectory { get; } = new();
    public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));
    public LaunchTimeoutArgument LaunchTimeout { get; } = new(TimeSpan.FromMinutes(5));
    public DeviceIdArgument DeviceId { get; } = new();
    public DeviceArchitectureArgument DeviceArchitecture { get; } = new();
    public ApiVersionArgument ApiVersion { get; } = new();

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
        TestPath,
        RuntimePath,
        TestAssembly,
        TestScript,
        OutputDirectory,
        Timeout,
        DeviceId,
        LaunchTimeout,
        DeviceArchitecture,
        ApiVersion,
    };
}
