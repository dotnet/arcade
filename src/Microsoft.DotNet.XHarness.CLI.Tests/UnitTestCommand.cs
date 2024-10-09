// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.Commands;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Tests;

internal class UnitTestCommand
{
    public static UnitTestCommand<UnitTestArguments<TArgument>> FromArgument<TArgument>(TArgument arg) where TArgument : Argument
    {
        return new UnitTestCommand<UnitTestArguments<TArgument>>(new UnitTestArguments<TArgument>(arg));
    }
}

internal class UnitTestCommand<TArguments> : XHarnessCommand<TArguments> where TArguments : XHarnessCommandArguments
{
    protected override string CommandUsage => "test";

    protected override string CommandDescription => "unit test command";

    public bool CommandRun { get; private set; }

    public IEnumerable<string> PassThroughArgs => PassThroughArguments;

    public IEnumerable<string> ExtraArgs => ExtraArguments;

    private readonly TArguments _arguments;
    protected override TArguments Arguments => _arguments;

    public UnitTestCommand(TArguments arguments, bool allowExtraArgs = false)
        : base(TargetPlatform.Apple, "unit-test", allowExtraArgs, new ServiceCollection())
    {
        _arguments = arguments;
    }

    protected override Task<ExitCode> InvokeInternal(ILogger logger)
    {
        CommandRun = true;
        return Task.FromResult(ExitCode.SUCCESS);
    }
}
