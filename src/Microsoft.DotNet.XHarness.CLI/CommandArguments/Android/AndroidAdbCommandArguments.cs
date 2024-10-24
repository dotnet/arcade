// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

internal class AndroidAdbCommandArguments : XHarnessCommandArguments
{
    public TimeoutArgument Timeout { get; set; } = new(TimeSpan.FromMinutes(1));

    protected override IEnumerable<Argument> GetArguments() => new[]
    {
        Timeout,
    };
}
