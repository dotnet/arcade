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

public interface IUninstallOrchestrator
{
    Task<ExitCode> OrchestrateAppUninstall(
        string bundleIdentifier,
        TestTargetOs target,
        string? deviceName,
        TimeSpan timeout,
        bool includeWirelessDevices,
        bool resetSimulator,
        bool enableLldb,
        CancellationToken cancellationToken);
}

/// <summary>
/// This orchestrator implements the `uninstall` command flow.
/// </summary>
public class UninstallOrchestrator : BaseOrchestrator, IUninstallOrchestrator
{
    public UninstallOrchestrator(
        IAppBundleInformationParser appBundleInformationParser,
        IAppInstaller appInstaller,
        IAppUninstaller appUninstaller,
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

    public Task<ExitCode> OrchestrateAppUninstall(
        string bundleIdentifier,
        TestTargetOs target,
        string? deviceName,
        TimeSpan timeout,
        bool includeWirelessDevices,
        bool resetSimulator,
        bool enableLldb,
        CancellationToken cancellationToken)
    {
        static Task<ExitCode> ExecuteMacCatalystApp(AppBundleInformation appBundleInfo)
            => throw new InvalidOperationException("uninstall command not available on maccatalyst");

        static Task<ExitCode> ExecuteApp(AppBundleInformation appBundleInfo, IDevice device, IDevice? companionDevice)
            => Task.FromResult(ExitCode.SUCCESS); // no-op

        return OrchestrateOperation(
            target,
            deviceName,
            includeWirelessDevices,
            resetSimulator,
            enableLldb,
            (target, device, cancellationToken) => GetAppBundleFromId(target, device, bundleIdentifier, cancellationToken),
            ExecuteMacCatalystApp,
            ExecuteApp,
            cancellationToken);
    }

    protected override Task<ExitCode> InstallApp(AppBundleInformation appBundleInfo, IDevice device, TestTargetOs target, CancellationToken cancellationToken)
        => Task.FromResult(ExitCode.SUCCESS); // no-op - we only want to uninstall the app

    protected override Task<ExitCode> UninstallApp(TestTarget target, string bundleIdentifier, IDevice device, bool isPreparation, CancellationToken cancellationToken)
    {
        // For the uninstallation, we don't want to uninstall twice so we skip the preparation one
        if (isPreparation)
        {
            return Task.FromResult(ExitCode.SUCCESS);
        }

        return base.UninstallApp(target, bundleIdentifier, device, isPreparation, cancellationToken);
    }
}
