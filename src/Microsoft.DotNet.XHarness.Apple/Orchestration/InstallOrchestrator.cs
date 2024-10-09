// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.Apple;

public interface IInstallOrchestrator
{
    Task<ExitCode> OrchestrateInstall(
        TestTargetOs target,
        string? deviceName,
        string appPackagePath,
        TimeSpan timeout,
        bool includeWirelessDevices,
        bool resetSimulator,
        bool enableLldb,
        CancellationToken cancellationToken);
}

/// <summary>
/// This orchestrator implements the `install` command flow.
/// </summary>
public class InstallOrchestrator : BaseOrchestrator, IInstallOrchestrator
{
    public InstallOrchestrator(
        IAppInstaller appInstaller,
        IAppUninstaller appUninstaller,
        IAppBundleInformationParser appBundleInformationParser,
        IDeviceFinder deviceFinder,
        ILogger consoleLogger,
        ILogs logs,
        IFileBackedLog mainLog,
        IErrorKnowledgeBase errorKnowledgeBase,
        IDiagnosticsData diagnosticsData,
        IHelpers helpers)
        : base(appBundleInformationParser, appInstaller, appUninstaller, deviceFinder, consoleLogger, logs, mainLog, errorKnowledgeBase, diagnosticsData, helpers)
    {
    }

    public async Task<ExitCode> OrchestrateInstall(
        TestTargetOs target,
        string? deviceName,
        string appPackagePath,
        TimeSpan timeout,
        bool includeWirelessDevices,
        bool resetSimulator,
        bool enableLldb,
        CancellationToken cancellationToken)
    {
        Task<AppBundleInformation> GetAppBundleInfo(TestTargetOs target, IDevice device, CancellationToken ct) => GetAppBundleFromPath(target, appPackagePath, ct);

        static Task<ExitCode> ExecuteMacCatalystApp(AppBundleInformation appBundleInfo)
            => throw new InvalidOperationException("install command not available on maccatalyst");

        static Task<ExitCode> ExecuteApp(AppBundleInformation appBundleInfo, IDevice device, IDevice? companionDevice)
            => Task.FromResult(ExitCode.SUCCESS); // no-op

        var result = await OrchestrateOperation(
            target,
            deviceName,
            includeWirelessDevices,
            resetSimulator,
            enableLldb,
            GetAppBundleInfo,
            ExecuteMacCatalystApp,
            ExecuteApp,
            cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return ExitCode.PACKAGE_INSTALLATION_TIMEOUT;
        }

        return result;
    }

    protected override Task CleanUpSimulators(IDevice device, IDevice? companionDevice)
        => Task.CompletedTask; // no-op so that we don't remove the app after (reset will only clean it up before)

    protected override Task<ExitCode> UninstallApp(TestTarget target, string bundleIdentifier, IDevice device, bool isPreparation, CancellationToken cancellationToken)
    {
        // For the installation, we want to uninstall during preparation only
        if (isPreparation)
        {
            return base.UninstallApp(target, bundleIdentifier, device, isPreparation, cancellationToken);
        }

        return Task.FromResult(ExitCode.SUCCESS);
    }
}
