// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;

namespace Microsoft.DotNet.XHarness.Apple;

public interface IAppInstaller
{
    Task<ProcessExecutionResult> InstallApp(
        AppBundleInformation appBundleInformation,
        TestTargetOs target,
        IDevice device,
        CancellationToken cancellationToken = default);
}

public class AppInstaller : IAppInstaller
{
    private readonly IMlaunchProcessManager _processManager;
    private readonly ILog _mainLog;

    public AppInstaller(IMlaunchProcessManager processManager, ILog mainLog)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
    }

    public async Task<ProcessExecutionResult> InstallApp(
        AppBundleInformation appBundleInformation,
        TestTargetOs target,
        IDevice device,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(appBundleInformation.LaunchAppPath))
        {
            throw new DirectoryNotFoundException("Failed to find the app bundle directory");
        }

        var args = new MlaunchArguments();

        if (target.Platform.IsSimulator())
        {
            args.Add(new SimulatorUDIDArgument(device));
            args.Add(new InstallAppOnSimulatorArgument(appBundleInformation.LaunchAppPath));
        }
        else
        {
            args.Add(new DeviceNameArgument(device));
            args.Add(new InstallAppOnDeviceArgument(appBundleInformation.LaunchAppPath));

            if (target.Platform.IsWatchOSTarget())
            {
                args.Add(new DeviceArgument("ios,watchos"));
            }
        }

        var totalSize = Directory.GetFiles(appBundleInformation.LaunchAppPath, "*", SearchOption.AllDirectories).Select((v) => new FileInfo(v).Length).Sum();
        _mainLog.WriteLine($"Installing '{appBundleInformation.LaunchAppPath}' to '{device.Name}' ({totalSize / 1024.0 / 1024.0:N2} MB)");

        return await _processManager.ExecuteCommandAsync(args, _mainLog, TimeSpan.FromMinutes(15), verbosity: 2, cancellationToken: cancellationToken);
    }
}
