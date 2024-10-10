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

internal class AppleResetSimulatorCommand : AppleAppCommand<AppleResetSimulatorCommandArguments>
{
    private const string CommandHelp = "Resets given iOS/tvOS simulator (wipes it clean)";

    protected override AppleResetSimulatorCommandArguments Arguments { get; } = new();
    protected override string CommandUsage { get; } = "apple reset-simulator --target=... --output-directory=... [OPTIONS]";
    protected override string CommandDescription { get; } = CommandHelp;

    public AppleResetSimulatorCommand(IServiceCollection services) : base("reset-simulator", false, services, CommandHelp)
    {
    }

    protected override Task<ExitCode> InvokeInternal(ServiceProvider serviceProvider, CancellationToken cancellationToken) =>
        serviceProvider.GetRequiredService<ISimulatorResetOrchestrator>()
            .OrchestrateSimulatorReset(Arguments.Target, Arguments.DeviceName, Arguments.Timeout, cancellationToken);
}
