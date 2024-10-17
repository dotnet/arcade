// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.AndroidHeadless;

internal class AndroidHeadlessUninstallCommandArguments : XHarnessCommandArguments
{
    public TestPathArgument TestPath { get; } = new();
    public RuntimePathArgument RuntimePath { get; } = new();
    public DeviceIdArgument DeviceId { get; } = new();

    protected override IEnumerable<Argument> GetArguments() => new Argument[]
    {
            TestPath,
            RuntimePath,
            DeviceId,
    };
}
