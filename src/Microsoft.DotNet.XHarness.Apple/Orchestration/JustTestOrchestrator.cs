// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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

public interface IJustTestOrchestrator
{
    Task<ExitCode> OrchestrateTest(
        string bundleIdentifier,
        TestTargetOs target,
        string? deviceName,
        TimeSpan timeout,
        TimeSpan launchTimeout,
        CommunicationChannel communicationChannel,
        XmlResultJargon xmlResultJargon,
        IEnumerable<string> singleMethodFilters,
        IEnumerable<string> classMethodFilters,
        bool includeWirelessDevices,
        bool enableLldb,
        bool signalAppEnd,
        IReadOnlyCollection<(string, string)> environmentalVariables,
        IEnumerable<string> passthroughArguments,
        CancellationToken cancellationToken);
}

/// <summary>
/// This orchestrator implements the `just-test` command flow.
/// This is the same as `test` except we only run an already installed application and
/// we don't prepare the device or clean up.
/// In this flow we need to connect to the running application over TCP and receive
/// the test results. We also need to watch timeouts better and parse the results
/// more comprehensively.
/// </summary>
public class JustTestOrchestrator : TestOrchestrator, IJustTestOrchestrator
{
    public JustTestOrchestrator(
        IAppBundleInformationParser appBundleInformationParser,
        IAppInstaller appInstaller,
        IAppUninstaller appUninstaller,
        IAppTesterFactory appTesterFactory,
        IDeviceFinder deviceFinder,
        ILogger consoleLogger,
        ILogs logs,
        IFileBackedLog mainLog,
        IErrorKnowledgeBase errorKnowledgeBase,
        IDiagnosticsData diagnosticsData,
        IHelpers helpers)
        : base(appBundleInformationParser, appInstaller, appUninstaller, appTesterFactory, deviceFinder, consoleLogger, logs, mainLog, errorKnowledgeBase, diagnosticsData, helpers)
    {
    }

    Task<ExitCode> IJustTestOrchestrator.OrchestrateTest(
        string bundleIdentifier,
        TestTargetOs target,
        string? deviceName,
        TimeSpan timeout,
        TimeSpan launchTimeout,
        CommunicationChannel communicationChannel,
        XmlResultJargon xmlResultJargon,
        IEnumerable<string> singleMethodFilters,
        IEnumerable<string> classMethodFilters,
        bool includeWirelessDevices,
        bool enableLldb,
        bool signalAppEnd,
        IReadOnlyCollection<(string, string)> environmentalVariables,
        IEnumerable<string> passthroughArguments,
        CancellationToken cancellationToken)
        => OrchestrateTest(
            (target, device, ct) => GetAppBundleFromId(target, device, bundleIdentifier, ct),
            target,
            deviceName,
            timeout,
            launchTimeout,
            communicationChannel,
            xmlResultJargon,
            singleMethodFilters,
            classMethodFilters,
            includeWirelessDevices,
            resetSimulator: false, // No simulator reset for just- commands
            enableLldb,
            signalAppEnd,
            environmentalVariables,
            passthroughArguments,
            cancellationToken);

    protected override Task CleanUpSimulators(IDevice device, IDevice? companionDevice)
        => Task.CompletedTask; // no-op so that we don't remove the app after (reset will only clean it up before)

    protected override Task<ExitCode> InstallApp(AppBundleInformation appBundleInfo, IDevice device, TestTargetOs target, CancellationToken cancellationToken)
        => Task.FromResult(ExitCode.SUCCESS); // no-op - we only want to run the app

    protected override Task<ExitCode> UninstallApp(TestTarget target, string bundleIdentifier, IDevice device, bool isPreparation, CancellationToken cancellationToken)
        => Task.FromResult(ExitCode.SUCCESS); // no-op - we only want to run the app
}
