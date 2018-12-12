using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Arcade.Sdk.Tests
{

    public class TestApp : IDisposable
    {
        private readonly string _binlogOutputDir;

        public TestApp(string workDir, string binlogOutputDir, string[] sourceDirectories)
        {
            WorkingDirectory = workDir;
            _binlogOutputDir = binlogOutputDir;
            Directory.CreateDirectory(workDir);

            foreach (var dir in sourceDirectories)
            {
                CopyRecursive(dir, workDir);
            }
        }

        public string WorkingDirectory { get; }

        public int ExecuteBuild(ITestOutputHelper output, params string[] scriptArgs)
        {
            var cmd = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? @".\build.cmd"
                    : "./build.sh";

            return ExecuteScript(output, cmd, scriptArgs);
        }

        private int ExecuteScript(ITestOutputHelper output, string fileName, string[] scriptArgs)
        {
            var binLogFile = Path.Combine(_binlogOutputDir, Path.GetFileName(WorkingDirectory), "build.binlog");

            output.WriteLine("Working dir   = " + WorkingDirectory);
            output.WriteLine("Binlog output = " + binLogFile);

            var cmd = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? "cmd.exe"
                    : "bash";

            var psi = new ProcessStartInfo
            {
                FileName = cmd,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,

                WorkingDirectory = WorkingDirectory,
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                psi.ArgumentList.Add("/C");
            }

            psi.ArgumentList.Add(fileName);
            psi.ArgumentList.AddRange(scriptArgs);

            psi.ArgumentList.Add("/bl:" + binLogFile);

            return Run(output, psi);
        }

        public int Run(ITestOutputHelper output, ProcessStartInfo psi)
        {
            void Write(object sender, DataReceivedEventArgs e)
            {
                output.WriteLine(e.Data ?? string.Empty);
            }

            psi.UseShellExecute = false;
            psi.RedirectStandardError = true;
            psi.RedirectStandardOutput = true;
            //psi.Environment["PATH"] = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");

            var process = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += Write;
            process.ErrorDataReceived += Write;
            output.WriteLine($"Starting: {process.StartInfo.FileName} {process.StartInfo.Arguments}");
            process.Start();
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            process.WaitForExit(1000 * 60 * 3);

            process.OutputDataReceived -= Write;
            process.ErrorDataReceived -= Write;
            return process.ExitCode;
        }

        private static void CopyRecursive(string srcDir, string destDir)
        {
            foreach (var srcFileName in Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories))
            {
                var destFileName = Path.Combine(destDir, srcFileName.Substring(srcDir.Length).TrimStart(new[] { Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar }));
                Directory.CreateDirectory(Path.GetDirectoryName(destFileName));
                File.Copy(srcFileName, destFileName);
            }
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(WorkingDirectory, recursive: true);
            }
            catch
            {
                // Sometimes antivirus scanning locks files and they can't be deleted. Retring after 500ms seems to get around this most of the time
                Thread.Sleep(500);
                Directory.Delete(WorkingDirectory, recursive: true);
            }
        }
    }
}
