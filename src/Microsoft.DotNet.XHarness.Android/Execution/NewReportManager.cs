using System;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.XHarness.Android.Execution;

class NewReportManager : IReportManager
{
    private readonly ILogger _log;
    public NewReportManager(ILogger log)
    {
        _log = log;
    }

    public string DumpBugReport(AdbRunner runner, string outputFilePathWithoutFormat)
    {
        // give some time for bug report to be available
        Thread.Sleep(3000);

        var result = runner.RunAdbCommand(new[] { "bugreport", $"{outputFilePathWithoutFormat}.zip" }, TimeSpan.FromMinutes(5));

        if (result.ExitCode != 0)
        {
            // Could throw here, but it would tear down a possibly otherwise acceptable execution.
            _log.LogError($"Error getting ADB bugreport:{Environment.NewLine}{result}");
            return string.Empty;
        }
        else
        {
            _log.LogInformation($"Wrote ADB bugreport to {outputFilePathWithoutFormat}.zip");
            return $"{outputFilePathWithoutFormat}.zip";
        }
    }
}
