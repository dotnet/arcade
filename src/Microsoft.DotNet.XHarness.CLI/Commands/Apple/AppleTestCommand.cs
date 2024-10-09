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
internal class AppleTestCommand : AppleAppCommand<AppleTestCommandArguments>
{
    private const string CommandHelp = "Installs, runs and uninstalls a given iOS/tvOS/watchOS/xrOS/MacCatalyst test application bundle containing TestRunner " +
        "in a target device/simulator.";

    protected override string CommandUsage { get; } = "apple test --app=... --output-directory=... --target=... [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
    protected override string CommandDescription { get; } = CommandHelp;
    protected override AppleTestCommandArguments Arguments { get; } = new();

    public AppleTestCommand(IServiceCollection services) : base("test", false, services, CommandHelp)
    {
    }

    protected override Task<ExitCode> InvokeInternal(ServiceProvider serviceProvider, CancellationToken cancellationToken) =>
        serviceProvider.GetRequiredService<ITestOrchestrator>()
            .OrchestrateTest(
                Arguments.AppBundlePath,
                Arguments.Target,
                Arguments.DeviceName,
                Arguments.Timeout,
                Arguments.LaunchTimeout,
                Arguments.CommunicationChannel,
                Arguments.XmlResultJargon,
                Arguments.SingleMethodFilters.Value,
                Arguments.ClassMethodFilters.Value,
                includeWirelessDevices: Arguments.IncludeWireless,
                resetSimulator: Arguments.ResetSimulator,
                enableLldb: Arguments.EnableLldb,
                signalAppEnd: Arguments.SignalAppEnd,
                Arguments.EnvironmentalVariables.Value,
                PassThroughArguments,
                cancellationToken);
}
