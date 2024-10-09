// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple.Simulators;

internal abstract class SimulatorsCommandArguments : XHarnessCommandArguments
{
    public XcodeArgument XcodeRoot { get; } = new();

    protected sealed override IEnumerable<Argument> GetArguments() =>
        GetAdditionalArguments().Concat(new Argument[]
        {
                XcodeRoot
        });

    protected abstract IEnumerable<Argument> GetAdditionalArguments();
}
