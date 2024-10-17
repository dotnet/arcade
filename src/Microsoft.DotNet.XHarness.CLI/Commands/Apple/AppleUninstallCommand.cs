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

internal class AppleUninstallCommand : AppleAppCommand<AppleUninstallCommandArguments>
{
    private const string CommandHelp = "Uninstalls a given iOS/tvOS/watchOS/xrOS/MacCatalyst application bundle from a target device/simulator";

    protected override AppleUninstallCommandArguments Arguments { get; } = new();
    protected override string CommandUsage { get; } = "apple uninstall --app=... --output-directory=... --target=... [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
    protected override string CommandDescription { get; } = CommandHelp;

    public AppleUninstallCommand(IServiceCollection services) : base("uninstall", false, services, CommandHelp)
    {
    }

    protected override Task<ExitCode> InvokeInternal(ServiceProvider serviceProvider, CancellationToken cancellationToken) =>
        serviceProvider.GetRequiredService<IUninstallOrchestrator>()
            .OrchestrateAppUninstall(
                Arguments.BundleIdentifier,
                Arguments.Target,
                Arguments.DeviceName,
                Arguments.Timeout,
                Arguments.IncludeWireless,
                resetSimulator: false,
                enableLldb: false,
                cancellationToken);
}
