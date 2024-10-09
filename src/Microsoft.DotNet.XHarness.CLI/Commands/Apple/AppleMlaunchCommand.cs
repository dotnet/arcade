// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple;

internal class AppleMlaunchCommand : AppleCommand<AppleMlaunchCommandArguments>
{
    private const string Description = "Invoke bundled mlaunch with given arguments";
    protected override string CommandUsage { get; } = "apple mlaunch [OPTIONS] -- [MLAUNCH ARGUMENTS]";
    protected override string CommandDescription => Description;

    protected override AppleMlaunchCommandArguments Arguments { get; } = new();

    public AppleMlaunchCommand(IServiceCollection services) : base("mlaunch", false, services, Description)
    {
    }

    protected override async Task<ExitCode> Invoke(ILogger logger)
    {
        if (!PassThroughArguments.Any())
        {
            logger.LogError("Please provide delimeter '--' followed by arguments for ADB:" + Environment.NewLine +
                $"    {CommandUsage}" + Environment.NewLine +
                $"Example:" + Environment.NewLine +
                $"    apple mlaunch --timeout 00:01:30 -- devices -l");

            return ExitCode.INVALID_ARGUMENTS;
        }

        var processManager = Services.BuildServiceProvider().GetRequiredService<IMlaunchProcessManager>();

        try
        {
            var nullLog = new CallbackLog(s => { });
            var stdout = new CallbackLog(Console.Write);
            var stderr = new CallbackLog(Console.Error.Write);

            var args = new MlaunchArguments(PassThroughArguments.Select(arg => new SimpleMlaunchArgument(arg)).ToArray());

            var cts = new CancellationTokenSource();
            cts.CancelAfter(Arguments.Timeout);

            var result = await processManager.ExecuteCommandAsync(
                args,
                Arguments.Verbosity < LogLevel.Information ? stdout : nullLog,
                Arguments.Verbosity <= LogLevel.Warning ? stdout : nullLog,
                stderr,
                Arguments.Timeout,
                Arguments.EnvironmentalVariables.Value.ToDictionary(t => t.Item1, t => t.Item2),
                verbosity: 0, // -v needs to be supplied by user
                cts.Token);

            if (result.TimedOut)
            {
                return ExitCode.TIMED_OUT;
            }

            return (ExitCode)result.ExitCode;
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
            return ExitCode.GENERAL_FAILURE;
        }
    }

    // This is needed because ProcessManagers accepts MlaunchArguments only which are strong-typed args supported by mlaunch
    // Since in this command, these are supplied by user, we need to forward them as-is
    private class SimpleMlaunchArgument : iOS.Shared.Execution.MlaunchArgument
    {
        private readonly string _argument;

        public SimpleMlaunchArgument(string argument)
        {
            _argument = argument;
        }

        public override string AsCommandLineArgument() => Escape(_argument);
    }
}
