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

internal class AppleInstallCommand : AppleAppCommand<AppleInstallCommandArguments>
{
    private const string CommandHelp = "Installs a given iOS/tvOS/watchOS/xrOS/MacCatalyst application bundle in a target device/simulator";

    protected override AppleInstallCommandArguments Arguments { get; } = new();

    protected override string CommandUsage { get; } = "apple install --app=... --output-directory=... --target=... [OPTIONS] [-- [RUNTIME ARGUMENTS]]";
    protected override string CommandDescription { get; } = CommandHelp;

    public AppleInstallCommand(IServiceCollection services) : base("install", false, services, CommandHelp)
    {
    }

    protected override Task<ExitCode> InvokeInternal(ServiceProvider serviceProvider, CancellationToken cancellationToken) =>
        serviceProvider.GetRequiredService<IInstallOrchestrator>()
            .OrchestrateInstall(
                Arguments.Target,
                Arguments.DeviceName,
                Arguments.AppBundlePath,
                Arguments.Timeout,
                Arguments.IncludeWireless,
                Arguments.ResetSimulator,
                enableLldb: false,
                cancellationToken);
}
