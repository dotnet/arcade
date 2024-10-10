// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments;

public interface IXHarnessCommandArguments
{
    DiagnosticsArgument Diagnostics { get; }
    VerbosityArgument Verbosity { get; set; }
    HelpArgument ShowHelp { get; }
    IEnumerable<Argument> GetCommandArguments();
    void Validate();
}

public abstract class XHarnessCommandArguments : IXHarnessCommandArguments
{
    public DiagnosticsArgument Diagnostics { get; } = new();
    public VerbosityArgument Verbosity { get; set; } = new(LogLevel.Information);
    public HelpArgument ShowHelp { get; } = new();

    public IEnumerable<Argument> GetCommandArguments() => GetArguments().Concat(new Argument[]
    {
        Diagnostics,
        Verbosity,
        ShowHelp,
    });

    public virtual void Validate()
    {
        foreach (var arg in GetCommandArguments())
        {
            arg.Validate();
        }
    }

    /// <summary>
    /// Returns additional option for your specific command.
    /// </summary>
    protected abstract IEnumerable<Argument> GetArguments();
}
