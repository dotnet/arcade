// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Hardware;

public class DefaultSimulatorSelectorTests
{
    private readonly Mock<IMlaunchProcessManager> _processManager;
    private readonly Mock<ITCCDatabase> _tccDatabase;
    private readonly DefaultSimulatorSelector _simulatorSelector;

    public DefaultSimulatorSelectorTests()
    {
        _processManager = new Mock<IMlaunchProcessManager>();
        _tccDatabase = new Mock<ITCCDatabase>();
        _simulatorSelector = new DefaultSimulatorSelector();
    }

    [Fact]
    public void SelectSimulatorTest()
    {
        var simulator1 = new SimulatorDevice(_processManager.Object, _tccDatabase.Object)
        {
            Name = "Simulator 1",
            UDID = "udid1",
            State = DeviceState.Shutdown,
        };

        var simulator2 = new SimulatorDevice(_processManager.Object, _tccDatabase.Object)
        {
            Name = "Simulator 2",
            UDID = "udid2",
            State = DeviceState.Booted,
        };

        var simulator3 = new SimulatorDevice(_processManager.Object, _tccDatabase.Object)
        {
            Name = "Simulator 3",
            UDID = "udid3",
            State = DeviceState.Booting,
        };

        var simulator = _simulatorSelector.SelectSimulator(new[] { simulator1, simulator2, simulator3 });

        // The Booted one
        Assert.Equal(simulator2, simulator);
    }
}
