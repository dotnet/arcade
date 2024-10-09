// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.Apple.Tests.Orchestration;

public class SimulatorResetOrchestratorTests : OrchestratorTestBase
{
    private readonly SimulatorResetOrchestrator _simulatorResetOrchestrator;

    public SimulatorResetOrchestratorTests()
    {
        _simulatorResetOrchestrator = new(
            _appInstaller.Object,
            _appUninstaller.Object,
            _deviceFinder.Object,
            _logger.Object,
            _logs,
            _mainLog.Object,
            _errorKnowledgeBase.Object,
            _diagnosticsData,
            _helpers.Object);

        _appInstaller.Reset();
        _appUninstaller.Reset();
    }

    [Fact]
    public async Task OrchestrateSimulatorResetTest()
    {
        // Setup
        var testTarget = new TestTargetOs(TestTarget.Simulator_iOS64, "13.5");

        // Act
        var result = await _simulatorResetOrchestrator.OrchestrateSimulatorReset(
            testTarget,
            SimulatorName,
            TimeSpan.FromMinutes(30),
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.SUCCESS, result);

        _deviceFinder.Verify(
            x => x.FindDevice(testTarget, SimulatorName, It.IsAny<ILog>(), false, true, It.IsAny<CancellationToken>()),
            Times.Once);

        VerifySimulatorReset(true);
        VerifySimulatorCleanUp(false);

        _appInstaller.VerifyNoOtherCalls();
        _appUninstaller.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TryDeviceResetTest()
    {
        // Setup
        var testTarget = new TestTargetOs(TestTarget.Device_iOS, "13.5");
        _deviceFinder.Reset();

        // Act
        var result = await _simulatorResetOrchestrator.OrchestrateSimulatorReset(
            testTarget,
            null,
            TimeSpan.FromMinutes(30),
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.INVALID_ARGUMENTS, result);

        _deviceFinder.VerifyNoOtherCalls();
        _appInstaller.VerifyNoOtherCalls();
        _appUninstaller.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TryMacCatalystResetTest()
    {
        // Setup
        var testTarget = new TestTargetOs(TestTarget.MacCatalyst, null);
        _deviceFinder.Reset();

        // Act
        var result = await _simulatorResetOrchestrator.OrchestrateSimulatorReset(
            testTarget,
            null,
            TimeSpan.FromMinutes(30),
            new CancellationToken());

        // Verify
        Assert.Equal(ExitCode.INVALID_ARGUMENTS, result);

        _deviceFinder.VerifyNoOtherCalls();
        _appInstaller.VerifyNoOtherCalls();
        _appUninstaller.VerifyNoOtherCalls();
    }
}
