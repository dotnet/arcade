// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

internal class AndroidInstallCommandArguments : XHarnessCommandArguments, IAndroidAppRunArguments
{
    public AppPathArgument AppPackagePath { get; } = new();
    public PackageNameArgument PackageName { get; } = new();
    public OutputDirectoryArgument OutputDirectory { get; } = new();
    public TimeoutArgument Timeout { get; } = new(TimeSpan.FromMinutes(15));
    public LaunchTimeoutArgument LaunchTimeout { get; } = new(TimeSpan.FromMinutes(5));
    public DeviceIdArgument DeviceId { get; } = new();
    public DeviceArchitectureArgument DeviceArchitecture { get; } = new();
    public ApiVersionArgument ApiVersion { get; } = new();

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
        AppPackagePath,
        PackageName,
        OutputDirectory,
        Timeout,
        DeviceId,
        LaunchTimeout,
        DeviceArchitecture,
        ApiVersion,
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
