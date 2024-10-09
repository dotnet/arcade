// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Android.Execution;

internal class Api23AndOlderReportManager : IReportManager
{
    private readonly ILogger _log;

    public Api23AndOlderReportManager(ILogger log)
    {
        _log = log;
    }

    public string DumpBugReport(AdbRunner runner, string outputFilePathWithoutFormat)
    {
        // give some time for bug report to be available
        Thread.Sleep(3000);

        var result = runner.RunAdbCommand(new[] { "bugreport" }, TimeSpan.FromMinutes(5));

        if (result.ExitCode != 0)
        {
            // Could throw here, but it would tear down a possibly otherwise acceptable execution.
            _log.LogError($"Error getting ADB bugreport:{Environment.NewLine}{result}");
            return string.Empty;
        }
        else
        {
            File.WriteAllText($"{outputFilePathWithoutFormat}.txt", result.StandardOutput);
            _log.LogInformation($"Wrote ADB bugreport to {outputFilePathWithoutFormat}.txt");
            return $"{outputFilePathWithoutFormat}.txt";
        }
    }
}
