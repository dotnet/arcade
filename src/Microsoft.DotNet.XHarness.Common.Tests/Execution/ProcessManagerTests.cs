// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;
using Xunit;

namespace Microsoft.DotNet.XHarness.Common.Tests.Execution;

public class ProcessManagerTests
{
    [Fact(Skip = "ping is not available in AzDO so this is rather for local development")]
    public async Task ProcessShouldBeKilled()
    {
        var pm = ProcessManagerFactory.CreateProcessManager();

        var process = new Process();
        process.StartInfo.FileName = "ping";
        process.StartInfo.Arguments = "-t 127.0.0.1";
        var log = new MemoryLog();

        var result = await pm.RunAsync(process, log, TimeSpan.FromSeconds(3));

        Assert.True(result.TimedOut);
    }
}
