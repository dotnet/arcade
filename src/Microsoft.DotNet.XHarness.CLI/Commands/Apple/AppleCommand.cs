// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.CLI.CommandArguments.Apple;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Commands.Apple;

internal abstract class AppleCommand<TArguments> : XHarnessCommand<TArguments> where TArguments : IAppleArguments
{
    protected AppleCommand(string name, bool allowsExtraArgs, IServiceCollection services, string? help = null)
        : base(TargetPlatform.Apple, name, allowsExtraArgs, services, help)
    {
    }

    protected sealed override Task<ExitCode> InvokeInternal(ILogger logger)
    {
        var processManager = new MlaunchProcessManager(Arguments.XcodeRoot, Arguments.MlaunchPath);
        Services.TryAddSingleton<IMlaunchProcessManager>(processManager);
        Services.TryAddSingleton<IMacOSProcessManager>(processManager);
        Services.TryAddSingleton<IProcessManager>(processManager);

        return Invoke(logger);
    }

    protected abstract Task<ExitCode> Invoke(ILogger logger);
}
