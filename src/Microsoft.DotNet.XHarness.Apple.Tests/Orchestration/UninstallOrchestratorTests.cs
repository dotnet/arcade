// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests.Orchestration;

public class UninstallOrchestratorTests : OrchestratorTestBase
{
    private readonly UninstallOrchestrator _uninstallOrchestrator;

    public UninstallOrchestratorTests()
    {
        _uninstallOrchestrator = new(
            _appBundleInformationParser.Object,
            _appInstaller.Object,
            _appUninstaller.Object,
            _deviceFinder.Object,
            _logger.Object,
            _logs,
            _mainLog.Object,
            _errorKnowledgeBase.Object,
            _diagnosticsData,
            _helpers.Object);
    }

    [Fact]
    public async Task OrchestrateSimulatorUninstallationTest()
    {
        // Setup
        _appUninstaller.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult
        {
            ExitCode = 0,
            TimedOut = false,
        }));

        var testTarget = new TestTargetOs(TestTarget.Simulator_iOS64, "13.5");

        // Act
        var result = await _uninstallOrchestrator.OrchestrateAppUninstall(
            BundleIdentifier,
            testTarget,
            SimulatorName,
            TimeSpan.FromMinutes(30),
            includeWirelessDevices: false,
            resetSimulator: false,
            enableLldb: false,
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);

        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, SimulatorName, It.IsAny<ILog>(), false, true, It.IsAny<CancellationToken>()),
            Times.Once);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);
        VerifyDiagnosticData(testTarget);

        _appInstaller.Verify(
            x => x.InstallApp(It.IsAny<AppBundleInformation>(), It.IsAny<TestTargetOs>(), It.IsAny<IDevice>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _appUninstaller.Verify(
            x => x.UninstallSimulatorApp(_simulator.Object, BundleIdentifier, It.IsAny<CancellationToken>()),
            Times.Once);

        _appUninstaller.Verify(
            x => x.UninstallDeviceApp(It.IsAny<IHardwareDevice>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _simulator
            .Verify(x => x.Boot(It.IsAny<ILog>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        _simulator
            .Verify(x => x.GetAppBundlePath(It.IsAny<ILog>(), BundleIdentifier, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task OrchestrateDeviceUninstallationTest()
    {
        // Setup
        _appUninstaller.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult
        {
            ExitCode = 0,
            TimedOut = false,
        }));

        var testTarget = new TestTargetOs(TestTarget.Device_tvOS, "14.2");

        // Act
        var result = await _uninstallOrchestrator.OrchestrateAppUninstall(
            BundleIdentifier,
            testTarget,
            null,
            TimeSpan.FromMinutes(30),
            includeWirelessDevices: false,
            resetSimulator: false,
            enableLldb: false,
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);

        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, null, It.IsAny<ILog>(), false, true, It.IsAny<CancellationToken>()),
            Times.Once);

        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);
        VerifyDiagnosticData(testTarget);

        _appInstaller.Verify(
            x => x.InstallApp(It.IsAny<AppBundleInformation>(), It.IsAny<TestTargetOs>(), It.IsAny<IDevice>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _appUninstaller.Verify(
            x => x.UninstallSimulatorApp(It.IsAny<ISimulatorDevice>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _appUninstaller.Verify(
            x => x.UninstallDeviceApp(_device.Object, BundleIdentifier, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OrchestrateSimulatorUninstallationWithResetTest()
    {
        // Setup
        _appUninstaller.SetReturnsDefault(Task.FromResult(new ProcessExecutionResult
        {
            ExitCode = 0,
            TimedOut = false,
        }));

        var testTarget = new TestTargetOs(TestTarget.Simulator_iOS64, "13.5");

        // Act
        var result = await _uninstallOrchestrator.OrchestrateAppUninstall(
            BundleIdentifier,
            testTarget,
            SimulatorName,
            TimeSpan.FromMinutes(30),
            includeWirelessDevices: false,
            resetSimulator: true,
            enableLldb: false,
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);

        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, SimulatorName, It.IsAny<ILog>(), false, true, It.IsAny<CancellationToken>()),
            Times.Once);

        VerifySimulatorReset(true);
        VerifySimulatorCleanUp(true);
        VerifyDiagnosticData(testTarget);

        _appInstaller.Verify(
            x => x.InstallApp(It.IsAny<AppBundleInformation>(), It.IsAny<TestTargetOs>(), It.IsAny<IDevice>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Verify that when resetting the device, we don't try to uninstall unnecessarily after
        _appUninstaller.Verify(
            x => x.UninstallSimulatorApp(It.IsAny<ISimulatorDevice>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _appUninstaller.Verify(
            x => x.UninstallDeviceApp(It.IsAny<IHardwareDevice>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OrchestrateMacCatalystUninstallationTest()
    {
        // Setup
        _appInstaller.Reset();
        _appUninstaller.Reset();
        _deviceFinder.Reset();

        var testTarget = new TestTargetOs(TestTarget.MacCatalyst, null);

        // Act
        await _uninstallOrchestrator.OrchestrateAppUninstall(
            BundleIdentifier,
            testTarget,
            null,
            TimeSpan.FromMinutes(30),
            includeWirelessDevices: false,
            resetSimulator: false,
            enableLldb: false,
            new CancellationToken());

        // Verify
        VerifySimulatorReset(false);
        VerifySimulatorCleanUp(false);

        _appInstaller.VerifyNoOtherCalls();
        _appUninstaller.VerifyNoOtherCalls();
        _deviceFinder.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OrchestrateDeviceUninstallationWhenNoDeviceTest()
    {
        // Setup
        _deviceFinder.Reset();
        _deviceFinder
            .Setup(x => x.FindDevice(
                It.IsAny<TestTargetOs>(),
                It.IsAny<string?>(),
                It.IsAny<ILog>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NoDeviceFoundException());

        var testTarget = new TestTargetOs(TestTarget.Device_iOS, "14.2");

        // Act
        var result = await _uninstallOrchestrator.OrchestrateAppUninstall(
            BundleIdentifier,
            testTarget,
            deviceName: null,
            TimeSpan.FromMinutes(30),
            includeWirelessDevices: false,
            resetSimulator: false,
            enableLldb: false,
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.DEVICE_NOT_FOUND, result);
    }
}
