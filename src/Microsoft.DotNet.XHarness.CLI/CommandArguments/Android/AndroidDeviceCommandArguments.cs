// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

internal class AndroidDeviceCommandArguments : XHarnessCommandArguments
{
    public OptionalAppPathArgument AppPackagePath { get; } = new();
    public DeviceArchitectureArgument DeviceArchitecture { get; } = new();
    public ApiVersionArgument ApiVersion { get; } = new();

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
        AppPackagePath,
        DeviceArchitecture,
        ApiVersion,
    };
}
