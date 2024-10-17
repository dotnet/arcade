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

public interface ISimulatorResetOrchestrator
{
    Task<ExitCode> OrchestrateSimulatorReset(
        TestTargetOs target,
        string? deviceName,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

/// <summary>
/// This orchestrator implements the `uninstall` command flow.
/// </summary>
public class SimulatorResetOrchestrator : BaseOrchestrator, ISimulatorResetOrchestrator
{
    private readonly ILogger _consoleLogger;

    public SimulatorResetOrchestrator(
        IAppInstaller appInstaller,
        IAppUninstaller appUninstaller,
        IDeviceFinder deviceFinder,
        ILogger consoleLogger,
        ILogs logs,
        IFileBackedLog mainLog,
        IErrorKnowledgeBase errorKnowledgeBase,
        IDiagnosticsData diagnosticsData,
        IHelpers helpers)
        : base(new FakeAppBundleInformationParser(), appInstaller, appUninstaller, deviceFinder, consoleLogger, logs, mainLog, errorKnowledgeBase, diagnosticsData, helpers)
    {
        _consoleLogger = consoleLogger;
    }

    public Task<ExitCode> OrchestrateSimulatorReset(
        TestTargetOs target,
        string? deviceName,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!target.Platform.IsSimulator() || target.Platform.ToRunMode() == RunMode.MacOS)
        {
            _consoleLogger.LogError($"The simulator reset action requires a simulator target while {target.AsString()} specified");
            return Task.FromResult(ExitCode.INVALID_ARGUMENTS);
        }

        static Task<ExitCode> ExecuteMacCatalystApp(AppBundleInformation appBundleInfo)
            => throw new InvalidOperationException("reset-simulator command not available on maccatalyst");

        static Task<ExitCode> ExecuteApp(AppBundleInformation appBundleInfo, IDevice device, IDevice? companionDevice)
            => Task.FromResult(ExitCode.SUCCESS); // no-op

        return OrchestrateOperation(
            target,
            deviceName,
            includeWirelessDevices: false,
            resetSimulator: true,
            enableLldb: false,
            (_, __, ___) => Task.FromResult(AppBundleInformation.FromBundleId(string.Empty)), // This is not really needed for this command
            ExecuteMacCatalystApp,
            ExecuteApp,
            cancellationToken);
    }

    protected override Task<ExitCode> InstallApp(AppBundleInformation appBundleInfo, IDevice device, TestTargetOs target, CancellationToken cancellationToken)
        => Task.FromResult(ExitCode.SUCCESS); // no-op - we only want to reset the simulator

    protected override Task<ExitCode> UninstallApp(TestTarget target, string bundleIdentifier, IDevice device, bool isPreparation, CancellationToken cancellationToken)
        => Task.FromResult(ExitCode.SUCCESS); // no-op - we only want to reset the simulator

    protected override Task CleanUpSimulators(IDevice device, IDevice? companionDevice)
        => Task.CompletedTask; // no-op - reset is enough, clean-up is not needed afterwards

    // The reset-simulator command doesn't (as oposed to the others) work with any app bundle specifically so we have to work around this part
    private class FakeAppBundleInformationParser : IAppBundleInformationParser
    {
        public Task<AppBundleInformation> ParseFromAppBundle(string appPackagePath, TestTarget target, ILog log, CancellationToken cancellationToken = default)
            => Task.FromResult(new AppBundleInformation(string.Empty, string.Empty, string.Empty, string.Empty, false));

        public Task<AppBundleInformation> ParseFromProject(string projectFilePath, TestTarget target, string buildConfiguration)
            => Task.FromResult(new AppBundleInformation(string.Empty, string.Empty, string.Empty, string.Empty, false));
    }
}
