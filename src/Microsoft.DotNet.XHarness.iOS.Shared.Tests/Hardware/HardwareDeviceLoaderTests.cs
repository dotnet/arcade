// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Hardware;

public class HardwareDeviceLoaderTests
{
    private readonly HardwareDeviceLoader _devices;
    private readonly Mock<IMlaunchProcessManager> _processManager;
    private readonly Mock<ILog> _executionLog;

    public HardwareDeviceLoaderTests()
    {
        _processManager = new Mock<IMlaunchProcessManager>();
        _devices = new HardwareDeviceLoader(_processManager.Object);
        _executionLog = new Mock<ILog>();
    }

    [Theory]
    [InlineData(false)] // no timeout
    [InlineData(true)] // timeoout
    public async Task LoadAsyncProcessErrorTest(bool timeout)
    {
        string processPath = null;
        MlaunchArguments passedArguments = null;

        // moq It.Is is not working as nicelly as we would like it, we capture data and use asserts
        _processManager.Setup(p => p.RunAsync(It.IsAny<Process>(), It.IsAny<MlaunchArguments>(), It.IsAny<ILog>(), It.IsAny<TimeSpan?>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken?>(), It.IsAny<bool?>()))
            .Returns<Process, MlaunchArguments, ILog, TimeSpan?, Dictionary<string, string>, int, CancellationToken?, bool?>((p, args, log, t, env, verbosity, token, d) =>
            {
                // we are going set the used args to validate them later, will always return an error from this method
                processPath = p.StartInfo.FileName;
                passedArguments = args;
                if (!timeout)
                {
                    return Task.FromResult(new ProcessExecutionResult { ExitCode = 1, TimedOut = false });
                }
                else
                {
                    return Task.FromResult(new ProcessExecutionResult { ExitCode = 0, TimedOut = true });
                }
            });

        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await _devices.LoadDevices(_executionLog.Object);
        });

        MlaunchArgument listDevArg = passedArguments.Where(a => a is ListDevicesArgument).FirstOrDefault();
        Assert.NotNull(listDevArg);

        MlaunchArgument outputFormatArg = passedArguments.Where(a => a is XmlOutputFormatArgument).FirstOrDefault();
        Assert.NotNull(outputFormatArg);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LoadAsyncProcessSuccess(bool extraData)
    {
        string processPath = null;
        MlaunchArguments passedArguments = null;

        // moq It.Is is not working as nicelly as we would like it, we capture data and use asserts
        _processManager.Setup(p => p.RunAsync(It.IsAny<Process>(), It.IsAny<MlaunchArguments>(), It.IsAny<ILog>(), It.IsAny<TimeSpan?>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken?>(), It.IsAny<bool?>()))
            .Returns<Process, MlaunchArguments, ILog, TimeSpan?, Dictionary<string, string>, int, CancellationToken?, bool?>((p, args, log, t, env, verbosity, token, d) =>
            {
                processPath = p.StartInfo.FileName;
                passedArguments = args;

                // we get the temp file that was passed as the args, and write our sample xml, which will be parsed to get the devices :)
                var tempPath = args.Where(a => a is ListDevicesArgument).First().AsCommandLineArgument();
                tempPath = tempPath.Substring(tempPath.IndexOf('=') + 1).Replace("\"", string.Empty);

                var name = GetType().Assembly.GetManifestResourceNames().Where(a => a.EndsWith("devices.xml", StringComparison.Ordinal)).FirstOrDefault();
                using (var outputStream = new StreamWriter(tempPath))
                using (var sampleStream = new StreamReader(GetType().Assembly.GetManifestResourceStream(name)))
                {
                    string line;
                    while ((line = sampleStream.ReadLine()) != null)
                    {
                        outputStream.WriteLine(line);
                    }
                }
                return Task.FromResult(new ProcessExecutionResult { ExitCode = 0, TimedOut = false });
            });

        await _devices.LoadDevices(_executionLog.Object, listExtraData: extraData);

        // assert the devices that are expected from the sample xml
        MlaunchArgument listDevArg = passedArguments.Where(a => a is ListDevicesArgument).FirstOrDefault();
        Assert.NotNull(listDevArg);

        MlaunchArgument outputFormatArg = passedArguments.Where(a => a is XmlOutputFormatArgument).FirstOrDefault();
        Assert.NotNull(outputFormatArg);

        if (extraData)
        {
            MlaunchArgument listExtraDataArg = passedArguments.Where(a => a is ListExtraDataArgument).FirstOrDefault();
            Assert.NotNull(listExtraDataArg);
        }

        Assert.Equal(2, _devices.Connected64BitIOS.Count());
        Assert.Single(_devices.Connected32BitIOS);
        Assert.Empty(_devices.ConnectedTV);
    }

    [Fact]
    public async Task FindAndCacheDevicesWithFailingMlaunchTest()
    {
        string processPath = null;
        MlaunchArguments passedArguments = null;

        // Moq.SetupSequence doesn't allow custom callbacks so we need to count ourselves
        var calls = 0;

        // moq It.Is is not working as nicelly as we would like it, we capture data and use asserts
        _processManager.Setup(p => p.RunAsync(It.IsAny<Process>(), It.IsAny<MlaunchArguments>(), It.IsAny<ILog>(), It.IsAny<TimeSpan?>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<int>(), It.IsAny<CancellationToken?>(), It.IsAny<bool?>()))
            .Returns<Process, MlaunchArguments, ILog, TimeSpan?, Dictionary<string, string>, int, CancellationToken?, bool?>((p, args, log, t, env, verbosity, token, d) =>
            {
                calls++;

                if (calls == 1)
                {
                    // Mlaunch can sometimes time out and we are testing that a subsequent Load will trigger it again
                    return Task.FromResult(new ProcessExecutionResult { ExitCode = 137, TimedOut = true });
                }

                processPath = p.StartInfo.FileName;
                passedArguments = args;

                // we get the temp file that was passed as the args, and write our sample xml, which will be parsed to get the devices :)
                var tempPath = args.Where(a => a is ListDevicesArgument).First().AsCommandLineArgument();
                tempPath = tempPath.Substring(tempPath.IndexOf('=') + 1).Replace("\"", string.Empty);

                var name = GetType().Assembly.GetManifestResourceNames().Where(a => a.EndsWith("devices.xml", StringComparison.Ordinal)).FirstOrDefault();
                using (var outputStream = new StreamWriter(tempPath))
                using (var sampleStream = new StreamReader(GetType().Assembly.GetManifestResourceStream(name)))
                {
                    string line;
                    while ((line = sampleStream.ReadLine()) != null)
                        outputStream.WriteLine(line);
                }
                return Task.FromResult(new ProcessExecutionResult { ExitCode = 0, TimedOut = false });
            });

        await Assert.ThrowsAsync<Exception>(async () => await _devices.LoadDevices(_executionLog.Object));

        Assert.Empty(_devices.ConnectedDevices);
        Assert.Equal(1, calls);
        await _devices.LoadDevices(_executionLog.Object);
        Assert.Equal(2, calls);
        Assert.NotEmpty(_devices.ConnectedDevices);
        await _devices.LoadDevices(_executionLog.Object);
        Assert.Equal(2, calls);
        await _devices.LoadDevices(_executionLog.Object);
        Assert.Equal(2, calls);
    }
}
