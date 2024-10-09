// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Hardware;

public class SimulatorDeviceTest
{
    private readonly Mock<ILog> _executionLog;
    private readonly Mock<IMlaunchProcessManager> _processManager;
    private readonly SimulatorDevice _simulator;

    public SimulatorDeviceTest()
    {
        _executionLog = new Mock<ILog>();
        _processManager = new Mock<IMlaunchProcessManager>();
        _simulator = new SimulatorDevice(_processManager.Object, new TCCDatabase(_processManager.Object))
        {
            UDID = Guid.NewGuid().ToString()
        };
    }

    [Theory]
    [InlineData("com.apple.CoreSimulator.SimRuntime.watchOS-5-1", true)]
    [InlineData("com.apple.CoreSimulator.SimRuntime.iOS-7-1", false)]
    public void IsWatchSimulatorTest(string runtime, bool expectation)
    {
        _simulator.SimRuntime = runtime;
        Assert.Equal(expectation, _simulator.IsWatchSimulator);
    }

    [Theory]
    [InlineData("com.apple.CoreSimulator.SimRuntime.iOS-12-1", "iOS 12.1")]
    [InlineData("com.apple.CoreSimulator.SimRuntime.iOS-10-1", "iOS 10.1")]
    public void OSVersionTest(string runtime, string expected)
    {
        _simulator.SimRuntime = runtime;
        Assert.Equal(expected, _simulator.OSVersion);
    }

    [Fact]
    public async Task EraseAsyncTest()
    {
        // just call and verify the correct args are pass
        await _simulator.Erase(_executionLog.Object);
        _processManager.Verify(h => h.ExecuteXcodeCommandAsync(It.Is<string>(s => s == "simctl"), It.Is<string[]>(args => args.Where(a => a == _simulator.UDID || a == "shutdown").Count() == 2), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()));
        _processManager.Verify(h => h.ExecuteXcodeCommandAsync(It.Is<string>(s => s == "simctl"), It.Is<string[]>(args => args.Where(a => a == _simulator.UDID || a == "erase").Count() == 2), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()));
        _processManager.Verify(h => h.ExecuteXcodeCommandAsync(It.Is<string>(s => s == "simctl"), It.Is<string[]>(args => args.Where(a => a == _simulator.UDID || a == "boot").Count() == 2), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()));
        _processManager.Verify(h => h.ExecuteXcodeCommandAsync(It.Is<string>(s => s == "simctl"), It.Is<string[]>(args => args.Where(a => a == _simulator.UDID || a == "shutdown").Count() == 2), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()));

    }

    [Fact]
    public async Task ShutdownAsyncTest()
    {
        await _simulator.Shutdown(_executionLog.Object);
        // just call and verify the correct args are pass
        _processManager.Verify(h => h.ExecuteXcodeCommandAsync(It.Is<string>(s => s == "simctl"), It.Is<string[]>(args => args.Where(a => a == _simulator.UDID || a == "shutdown").Count() == 2), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()));
    }

    [Fact(Skip = "Running this test will actually kill simulators on the machine")]
    public async Task KillEverythingAsyncTest()
    {
        Func<IList<string>, bool> verifyKillAll = (args) =>
        {
            var toKill = new List<string> { "-9", "iPhone Simulator", "iOS Simulator", "Simulator", "Simulator (Watch)", "com.apple.CoreSimulator.CoreSimulatorService", "ibtoold" };
            return args.Where(a => toKill.Contains(a)).Count() == toKill.Count;
        };

        var simulator = new SimulatorDevice(_processManager.Object, new TCCDatabase(_processManager.Object));
        await simulator.KillEverything(_executionLog.Object);

        // verify that all the diff process have been killed making sure args are correct
        _processManager.Verify(p => p.ExecuteCommandAsync(It.Is<string>(s => s == "launchctl"), It.Is<string[]>(args => args.Where(a => a == "remove" || a == "com.apple.CoreSimulator.CoreSimulatorService").Count() == 2), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), null, null));
        _processManager.Verify(p => p.ExecuteCommandAsync(It.Is<string>(s => s == "killall"), It.Is<IList<string>>(a => verifyKillAll(a)), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), null, null));
    }

}
