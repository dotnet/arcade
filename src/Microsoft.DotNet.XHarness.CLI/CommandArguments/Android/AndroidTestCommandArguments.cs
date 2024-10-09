// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

internal class AndroidTestCommandArguments : XHarnessCommandArguments, IAndroidAppRunArguments
{
    public AppPathArgument AppPackagePath { get; } = new();
    public PackageNameArgument PackageName { get; } = new();
    public OutputDirectoryArgument OutputDirectory { get; } = new();
    public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));
    public LaunchTimeoutArgument LaunchTimeout { get; } = new(TimeSpan.FromMinutes(5));
    public DeviceIdArgument DeviceId { get; } = new();
    public DeviceArchitectureArgument DeviceArchitecture { get; } = new();
    public ApiVersionArgument ApiVersion { get; } = new();
    public InstrumentationNameArgument InstrumentationName { get; } = new();
    public InstrumentationArguments InstrumentationArguments { get; } = new();
    public ExpectedExitCodeArgument ExpectedExitCode { get; } = new((int)Common.CLI.ExitCode.SUCCESS);
    public DeviceOutputFolderArgument DeviceOutputFolder { get; } = new();
    public WifiArgument Wifi { get; } = new();

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
        AppPackagePath,
        PackageName,
        OutputDirectory,
        Timeout,
        LaunchTimeout,
        DeviceArchitecture,
        DeviceId,
        ApiVersion,
        InstrumentationName,
        InstrumentationArguments,
        ExpectedExitCode,
        DeviceOutputFolder,
        Wifi,
    };

    public override void Validate()
    {
        base.Validate();

        if (!File.Exists(AppPackagePath))
        {
            throw new ArgumentException($"Couldn't find {AppPackagePath}!");
        }
    }
}
