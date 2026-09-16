using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.DotNet.Helix.JobSender.Test;

public class LegacyRunnerCompatProcessTests
{
    [Fact]
    public async Task CompatibilityModulesPassPythonBehaviorTests()
    {
        var testPath = Path.Combine(AppContext.BaseDirectory, "LegacyRunnerCompatTests.py");
        var startInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "python" : "python3",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(testPath);

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(
            process.ExitCode == 0,
            $"Python compatibility tests failed with exit code {process.ExitCode}.{Environment.NewLine}" +
            $"stdout:{Environment.NewLine}{await standardOutput}{Environment.NewLine}" +
            $"stderr:{Environment.NewLine}{await standardError}");
    }
}
