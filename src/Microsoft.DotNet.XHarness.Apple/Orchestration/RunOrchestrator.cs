// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.Apple;

public interface IRunOrchestrator
{
    Task<ExitCode> OrchestrateRun(
        string appBundlePath,
        TestTargetOs target,
        string? deviceName,
        TimeSpan timeout,
        TimeSpan launchTimeout,
        int expectedExitCode,
        bool includeWirelessDevices,
        bool resetSimulator,
        bool enableLldb,
        bool signalAppEnd,
        bool waitForExit,
        IReadOnlyCollection<(string, string)> environmentalVariables,
        IEnumerable<string> passthroughArguments,
        CancellationToken cancellationToken);
}

/// <summary>
/// This orchestrator implements the `run` command flow.
/// In this flow we spawn the application and do not expect TestRunner inside.
/// We only try to detect the exit code after the app run is finished.
/// </summary>
public class RunOrchestrator : BaseOrchestrator, IRunOrchestrator
{
    private readonly IiOSExitCodeDetector _iOSExitCodeDetector;
    private readonly IMacCatalystExitCodeDetector _macCatalystExitCodeDetector;
    private readonly ILogger _logger;
    private readonly ILogs _logs;
    private readonly IErrorKnowledgeBase _errorKnowledgeBase;
    private readonly IAppRunner _appRunner;

    private bool _waitForExit = true;

    public RunOrchestrator(
        IAppBundleInformationParser appBundleInformationParser,
        IAppInstaller appInstaller,
        IAppUninstaller appUninstaller,
        IAppRunnerFactory appRunnerFactory,
        IDeviceFinder deviceFinder,
        IiOSExitCodeDetector iOSExitCodeDetector,
        IMacCatalystExitCodeDetector macCatalystExitCodeDetector,
        ILogger consoleLogger,
        ILogs logs,
        IFileBackedLog mainLog,
        IErrorKnowledgeBase errorKnowledgeBase,
        IDiagnosticsData diagnosticsData,
        IHelpers helpers)
        : base(appBundleInformationParser, appInstaller, appUninstaller, deviceFinder, consoleLogger, logs, mainLog, errorKnowledgeBase, diagnosticsData, helpers)
    {
        _iOSExitCodeDetector = iOSExitCodeDetector ?? throw new ArgumentNullException(nameof(iOSExitCodeDetector));
        _macCatalystExitCodeDetector = macCatalystExitCodeDetector ?? throw new ArgumentNullException(nameof(macCatalystExitCodeDetector));
        _logger = consoleLogger ?? throw new ArgumentNullException(nameof(consoleLogger));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _errorKnowledgeBase = errorKnowledgeBase ?? throw new ArgumentNullException(nameof(errorKnowledgeBase));

        // Only add the extra callback if we do know that the feature was indeed enabled
        Action<string>? logCallback = IsLldbEnabled() ? (l) => NotifyUserLldbCommand(_logger, l) : null;
        _appRunner = appRunnerFactory.Create(mainLog, logs, logCallback);
    }

    public Task<ExitCode> OrchestrateRun(
        string appBundlePath,
        TestTargetOs target,
        string? deviceName,
        TimeSpan timeout,
        TimeSpan launchTimeout,
        int expectedExitCode,
        bool includeWirelessDevices,
        bool resetSimulator,
        bool enableLldb,
        bool signalAppEnd,
        bool waitForExit,
        IReadOnlyCollection<(string, string)> environmentalVariables,
        IEnumerable<string> passthroughArguments,
        CancellationToken cancellationToken)
        => OrchestrateRun(
            (target, device, ct) => GetAppBundleFromPath(target, appBundlePath, ct),
            target,
            deviceName,
            timeout: timeout,
            launchTimeout: launchTimeout,
            expectedExitCode,
            includeWirelessDevices: includeWirelessDevices,
            resetSimulator: resetSimulator,
            enableLldb: enableLldb,
            signalAppEnd: signalAppEnd,
            waitForExit: waitForExit,
            environmentalVariables,
            passthroughArguments,
            cancellationToken);

    protected override Task<ExitCode> UninstallApp(TestTarget target, string bundleIdentifier, IDevice device, bool isPreparation, CancellationToken cancellationToken)
    {
        if (!_waitForExit && !isPreparation)
        {
            return Task.FromResult(ExitCode.SUCCESS);
        }

        return base.UninstallApp(target, bundleIdentifier, device, isPreparation, cancellationToken);
    }

    protected override Task CleanUpSimulators(IDevice device, IDevice? companionDevice)
    {
        if (!_waitForExit)
        {
            return Task.FromResult(ExitCode.SUCCESS);
        }

        return base.CleanUpSimulators(device, companionDevice);
    }

    protected async Task<ExitCode> OrchestrateRun(
        GetAppBundleInfoFunc getAppBundleInfo,
        TestTargetOs target,
        string? deviceName,
        TimeSpan timeout,
        TimeSpan launchTimeout,
        int expectedExitCode,
        bool includeWirelessDevices,
        bool resetSimulator,
        bool enableLldb,
        bool signalAppEnd,
        bool waitForExit,
        IReadOnlyCollection<(string, string)> environmentalVariables,
        IEnumerable<string> passthroughArguments,
        CancellationToken cancellationToken)
    {
        if (signalAppEnd && !waitForExit)
        {
            throw new InvalidOperationException("Cannot receive app end signal without waiting for it to exit");
        }

        _waitForExit = waitForExit;

        // The --launch-timeout option must start counting now and not complete before we start running tests to succeed.
        // After then, this timeout must not interfere.
        // Tests start running inside of ExecuteApp() which means we have to time-constrain all operations happening inside
        // OrchestrateRun() that happen before we start the app execution.
        // We will achieve this by sending a special cancellation token to OrchestrateRun() and only cancel if it in case
        // we didn't manage to start the app run until then.
        using var launchTimeoutCancellation = new CancellationTokenSource();
        var appRunStarted = false;
        var task = Task.Delay(launchTimeout < timeout ? launchTimeout : timeout, cancellationToken).ContinueWith(t =>
        {
            if (!appRunStarted)
            {
                _logger.LogError("Cancelling the run as application failed to launch in time");
                launchTimeoutCancellation.Cancel();
            }
        }, cancellationToken);

        using var launchTimeoutCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
            launchTimeoutCancellation.Token,
            cancellationToken);

        Task<ExitCode> ExecuteMacCatalystApp(AppBundleInformation appBundleInfo)
        {
            appRunStarted = true;
            return this.ExecuteMacCatalystApp(
                appBundleInfo,
                timeout,
                expectedExitCode,
                signalAppEnd,
                waitForExit,
                environmentalVariables,
                passthroughArguments,
                cancellationToken);
        }

        Task<ExitCode> ExecuteApp(AppBundleInformation appBundleInfo, IDevice device, IDevice? companionDevice)
        {
            // Exit code detection is broken on the newest iOS
            // More details here: https://github.com/dotnet/xharness/issues/819
            if (expectedExitCode != 0)
            {
                var os = target.Platform.IsSimulator() ? device.OSVersion.Split(' ', 2).Last() : device.OSVersion;
                if (Version.TryParse(os, out var version) && version.Major > 14)
                {
                    _logger.LogWarning(
                        "Exit code detection is not working on iOS/tvOS 15+ so the run will fail to match it with the expected value");
                }
            }

            appRunStarted = true;
            return this.ExecuteApp(
                appBundleInfo,
                target,
                device,
                companionDevice,
                timeout,
                expectedExitCode,
                signalAppEnd: signalAppEnd,
                waitForExit: waitForExit,
                environmentalVariables,
                passthroughArguments,
                cancellationToken);
        }

        return await OrchestrateOperation(
            target,
            deviceName,
            includeWirelessDevices: includeWirelessDevices,
            resetSimulator: resetSimulator,
            enableLldb: enableLldb,
            getAppBundleInfo,
            ExecuteMacCatalystApp,
            ExecuteApp,
            launchTimeoutCancellationToken.Token);
    }

    private async Task<ExitCode> ExecuteApp(
        AppBundleInformation appBundleInfo,
        TestTargetOs target,
        IDevice device,
        IDevice? companionDevice,
        TimeSpan timeout,
        int expectedExitCode,
        bool signalAppEnd,
        bool waitForExit,
        IReadOnlyCollection<(string, string)> environmentalVariables,
        IEnumerable<string> passthroughArguments,
        CancellationToken cancellationToken)
    {
        if (signalAppEnd && target.Platform.IsSimulator())
        {
            _logger.LogWarning("The --signal-app-end option is used for device tests and has no effect on simulators");
        }

        _logger.LogInformation($"Starting '{appBundleInfo.AppName}' on '{device.Name}'");

        ProcessExecutionResult result = await _appRunner.RunApp(
            appBundleInfo,
            target,
            device,
            companionDevice,
            timeout,
            signalAppEnd: signalAppEnd,
            waitForExit: waitForExit,
            passthroughArguments,
            environmentalVariables,
            cancellationToken);

        if (!waitForExit)
        {
            _logger.LogInformation("Not waiting for app to exit");
            return ExitCode.SUCCESS;
        }

        return ParseResult(_iOSExitCodeDetector, expectedExitCode, appBundleInfo, result);
    }

    private async Task<ExitCode> ExecuteMacCatalystApp(
        AppBundleInformation appBundleInfo,
        TimeSpan timeout,
        int expectedExitCode,
        bool signalAppEnd,
        bool waitForExit,
        IReadOnlyCollection<(string, string)> environmentalVariables,
        IEnumerable<string> passthroughArguments,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Starting '{appBundleInfo.AppName}' on MacCatalyst");

        ProcessExecutionResult result = await _appRunner.RunMacCatalystApp(
            appBundleInfo,
            timeout,
            signalAppEnd: signalAppEnd,
            waitForExit: waitForExit,
            passthroughArguments,
            environmentalVariables,
            cancellationToken: cancellationToken);

        if (!waitForExit)
        {
            _logger.LogInformation("Not waiting for app to exit");
            return ExitCode.SUCCESS;
        }

        return ParseResult(_macCatalystExitCodeDetector, expectedExitCode, appBundleInfo, result);
    }

    private ExitCode ParseResult(
        IExitCodeDetector exitCodeDetector,
        int expectedExitCode,
        AppBundleInformation appBundleInfo,
        ProcessExecutionResult result)
    {
        if (result.TimedOut)
        {
            _logger.LogError($"App run has timed out");
            return ExitCode.TIMED_OUT;
        }

        if (!result.Succeeded)
        {
            _logger.LogError($"App run has failed. mlaunch exited with {result.ExitCode}");
            return ExitCode.APP_LAUNCH_FAILURE;
        }

        var logs = _logs.Where(log => log.Description == LogType.SystemLog.ToString() || log.Description == LogType.ApplicationLog.ToString()).ToList();
        if (!logs.Any())
        {
            _logger.LogError("Application has finished but no system log found. Failed to determine the exit code!");
            return ExitCode.RETURN_CODE_NOT_SET;
        }

        int? exitCode = null;
        foreach (var log in logs)
        {
            try
            {
                exitCode = exitCodeDetector.DetectExitCode(appBundleInfo, log);

                if (exitCode.HasValue)
                {
                    _logger.LogDebug($"Detected exit code {exitCode.Value} from {log.FullPath}");
                    break;
                }

                _logger.LogDebug($"Failed to determine the exit code from {log.FullPath}");
            }
            catch (Exception e)
            {
                _logger.LogDebug($"Failed to determine the exit code from {log.FullPath}:{Environment.NewLine}{e.Message}");
            }
        }

        if (exitCode is null)
        {
            if (expectedExitCode != 0)
            {
                _logger.LogError("Application has finished but XHarness failed to determine its exit code!");
                return ExitCode.RETURN_CODE_NOT_SET;
            }

            _logger.LogInformation("App run ended, no abnormal exit code detected (0 assumed)");
            exitCode = 0;
        }
        else
        {
            _logger.LogInformation($"App run ended with {exitCode}");
        }

        if (expectedExitCode != exitCode)
        {
            _logger.LogError($"Application has finished with exit code {exitCode} but {expectedExitCode} was expected");
            var cliExitCode = ExitCode.GENERAL_FAILURE;

            foreach (var log in _logs)
            {
                if (_errorKnowledgeBase.IsKnownTestIssue(log, out var failure))
                {
                    _logger.LogError(failure.HumanMessage);

                    if (failure.SuggestedExitCode.HasValue)
                    {
                        cliExitCode = (ExitCode)failure.SuggestedExitCode.Value;
                    }

                    break;
                }
            }

            return cliExitCode;
        }

        _logger.LogInformation("Application has finished with exit code: " + exitCode +
            (expectedExitCode != 0 ? " (as expected)" : null));

        return ExitCode.SUCCESS;
    }
}
