// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Hardware;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Hardware;

public class TCCDatabaseTests
{
    private readonly Mock<IMlaunchProcessManager> _processManager;
    private readonly TCCDatabase _database;
    private readonly Mock<ILog> _executionLog;
    private readonly string _simRuntime;
    private readonly string _dataPath;
    private readonly string _udid;

    public TCCDatabaseTests()
    {
        _processManager = new Mock<IMlaunchProcessManager>();
        _database = new TCCDatabase(_processManager.Object);
        _executionLog = new Mock<ILog>();
        _simRuntime = "com.apple.CoreSimulator.SimRuntime.iOS-12-1";
        _dataPath = "/path/to/my/data";
        _udid = "D9DCBED1EC414ECE9A2353364C2AC454";
    }

    [Theory]
    [InlineData("com.apple.CoreSimulator.SimRuntime.iOS-12-1", 3)]
    [InlineData("com.apple.CoreSimulator.SimRuntime.iOS-10-1", 2)]
    [InlineData("com.apple.CoreSimulator.SimRuntime.iOS-9-1", 2)]
    [InlineData("com.apple.CoreSimulator.SimRuntime.iOS-7-1", 1)]
    [InlineData("com.apple.CoreSimulator.SimRuntime.tvOS-12-3", 3)]
    [InlineData("com.apple.CoreSimulator.SimRuntime.tvOS-8-1", 2)]
    [InlineData("com.apple.CoreSimulator.SimRuntime.watchOS-5-1", 3)]
    [InlineData("com.apple.CoreSimulator.SimRuntime.watchOS-4-1", 2)]
    [InlineData("com.apple.CoreSimulator.SimRuntime.iOS-14-0", 4)]
    [InlineData("com.apple.CoreSimulator.SimRuntime.tvOS-14-0", 4)]
    [InlineData("com.apple.CoreSimulator.SimRuntime.watchOS-7-0", 4)]
    public void GetTCCFormatTest(string runtime, int expected) => Assert.Equal(expected, _database.GetTCCFormat(runtime));

    [Fact]
    public void GetTCCFormatUnknownTest() => Assert.Throws<NotImplementedException>(() => _database.GetTCCFormat("unknown-sim-runtime"));

    [Fact]
    public async Task AgreeToPromptsAsyncNoIdentifiers()
    {
        // we should write in the log that we did not managed to agree to it
        _executionLog.Setup(l => l.WriteLine(It.IsAny<string>()));
        await _database.AgreeToPromptsAsync(_simRuntime, _dataPath, _udid, _executionLog.Object);
        _executionLog.Verify(l => l.WriteLine("No bundle identifiers given when requested permission editing."));
    }

    [Fact]
    public async Task AgreeToPropmtsAsyncTimeoutsTest()
    {
        string processName = null;
        // set the process manager to always return a failure so that we do eventually get a timeout
        _processManager.Setup(p => p.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), null, null))
            .Returns<string, IList<string>, ILog, TimeSpan, Dictionary<string, string>, CancellationToken?>((p, a, l, t, e, c) =>
            {
                processName = p;
                return Task.FromResult(new ProcessExecutionResult { ExitCode = 1, TimedOut = true });
            });
        // try to accept and fail because we always timeout
        await _database.AgreeToPromptsAsync(_simRuntime, _dataPath, _udid, _executionLog.Object, "my-bundle-id", "your-bundle-id");

        // verify that we did write in the logs and that we did call sqlite3
        Assert.Equal("sqlite3", processName);
        _executionLog.Verify(l => l.WriteLine("Failed to edit TCC.db, the test run might hang due to permission request dialogs"), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("com.apple.CoreSimulator.SimRuntime.iOS-12-1", 3)]
    [InlineData("com.apple.CoreSimulator.SimRuntime.iOS-10-1", 2)]
    [InlineData("com.apple.CoreSimulator.SimRuntime.iOS-7-1", 1)]
    public async Task AgreeToPromptsAsyncSuccessTest(string runtime, int dbVersion)
    {
        string bundleIdentifier = "my-bundle-identifier";
        var services = new string[] {
                    "kTCCServiceAll",
                    "kTCCServiceAddressBook",
                    "kTCCServiceCalendar",
                    "kTCCServiceCamera",
                    "kTCCServicePhotos",
                    "kTCCServiceMediaLibrary",
                    "kTCCServiceMicrophone",
                    "kTCCServiceUbiquity",
                    "kTCCServiceWillow"
                };
        var expectedArgs = new StringBuilder("\n");
        // assert the sql used depending on the version
        foreach (var id in new[] { bundleIdentifier, bundleIdentifier + ".watchkitapp" })
        {
            switch (dbVersion)
            {
                case 1:
                    foreach (var s in services)
                    {
                        expectedArgs.AppendFormat("DELETE FROM access WHERE service = '{0}' AND client = '{1}';\n", s, id);
                        expectedArgs.AppendFormat("INSERT INTO access VALUES('{0}','{1}',0,1,0,NULL);\n", s, id);
                    }
                    break;
                case 2:
                    foreach (var s in services)
                    {
                        expectedArgs.AppendFormat("DELETE FROM access WHERE service = '{0}' AND client = '{1}';\n", s, id);
                        expectedArgs.AppendFormat("INSERT INTO access VALUES('{0}','{1}',0,1,0,NULL,NULL);\n", s, id);
                    }
                    break;
                case 3:
                    foreach (var s in services)
                    {
                        expectedArgs.AppendFormat("INSERT OR REPLACE INTO access VALUES('{0}','{1}',0,1,0,NULL,NULL,NULL,'UNUSED',NULL,NULL,{2});\n", s, id, DateTimeOffset.Now.ToUnixTimeSeconds());
                    }
                    break;
            }
        }
        string processName = null;
        IList<string> args = new List<string>();
        _processManager.Setup(p => p.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<ILog>(), It.IsAny<TimeSpan>(), null, null))
            .Returns<string, IList<string>, ILog, TimeSpan, Dictionary<string, string>, CancellationToken?>((p, a, l, t, e, c) =>
            {
                processName = p;
                args = a;
                return Task.FromResult(new ProcessExecutionResult { ExitCode = 0, TimedOut = false });
            });

        await _database.AgreeToPromptsAsync(runtime, _dataPath, _udid, _executionLog.Object, bundleIdentifier);

        Assert.Equal("sqlite3", processName);
        // assert that the sql is present
        Assert.Contains(_dataPath, args);
        Assert.Contains(expectedArgs.ToString(), args);
    }
}
