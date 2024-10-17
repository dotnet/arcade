// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.CLI.CommandArguments;
using Microsoft.DotNet.XHarness.CLI.Commands;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.CLI.Android;

internal abstract class AndroidCommand<TArguments> : XHarnessCommand<TArguments> where TArguments : IXHarnessCommandArguments
{
    protected readonly Lazy<IDiagnosticsData> _diagnosticsData;
    protected IDiagnosticsData DiagnosticsData => _diagnosticsData.Value;

    protected AndroidCommand(string name, bool allowsExtraArgs, string? help = null)
        : base(TargetPlatform.Android, name, allowsExtraArgs, new ServiceCollection(), help)
    {
        _diagnosticsData = new(() => Services.BuildServiceProvider().GetRequiredService<IDiagnosticsData>());
    }

    protected sealed override Task<ExitCode> InvokeInternal(ILogger logger)
    {
        try
        {
            return Task.FromResult(InvokeCommand(logger));
        }
        catch (NoDeviceFoundException noDevice)
        {
            logger.LogCritical(noDevice.Message);
            return Task.FromResult(ExitCode.DEVICE_NOT_FOUND);
        }
        catch (AdbFailureException adbFailure)
        {
            logger.LogCritical(adbFailure, adbFailure.Message);
            return Task.FromResult(ExitCode.ADB_FAILURE);
        }
        catch (Exception toLog)
        {
            logger.LogCritical(toLog, toLog.Message);
        }

        return Task.FromResult(ExitCode.GENERAL_FAILURE);
    }

    protected abstract ExitCode InvokeCommand(ILogger logger);
}
