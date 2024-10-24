// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Apple;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple;

/// <summary>
/// Command which executes a given, already-packaged iOS application, waits on it and returns status based on the outcome.
/// </summary>
internal class AppleRunCommand : AppleAppCommand<AppleRunCommandArguments>
{
    private const string CommandHelp = "Installs, runs and uninstalls a given iOS/tvOS/watchOS/xrOS/MacCatalyst application bundle " +
        "in a target device/simulator and tries to detect the exit code.";

    protected override AppleRunCommandArguments Arguments { get; } = new();
    protected override string CommandUsage { get; } = "apple run --app=... --output-directory=... --target=... [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
    protected override string CommandDescription { get; } = CommandHelp;

    public AppleRunCommand(IServiceCollection services) : base("run", false, services, CommandHelp)
    {
    }

    protected override Task<ExitCode> InvokeInternal(ServiceProvider serviceProvider, CancellationToken cancellationToken) =>
        serviceProvider.GetRequiredService<IRunOrchestrator>()
            .OrchestrateRun(
                appBundlePath: Arguments.AppBundlePath,
                target: Arguments.Target,
                deviceName: Arguments.DeviceName,
                timeout: Arguments.Timeout,
                launchTimeout: Arguments.LaunchTimeout,
                expectedExitCode: Arguments.ExpectedExitCode,
                includeWirelessDevices: Arguments.IncludeWireless,
                resetSimulator: Arguments.ResetSimulator,
                enableLldb: Arguments.EnableLldb,
                signalAppEnd: Arguments.SignalAppEnd,
                waitForExit: !Arguments.NoWait,
                environmentalVariables: Arguments.EnvironmentalVariables.Value,
                passthroughArguments: PassThroughArguments,
                cancellationToken);
}
