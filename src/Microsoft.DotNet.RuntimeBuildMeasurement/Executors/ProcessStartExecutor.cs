using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.RuntimeBuildMeasurement.Executors
{
    public class ProcessStartExecutor : IExecutor
    {
        public Task Execute(DirectoryInfo workingDirectory, string name, string args = "")
            => RunProcess(workingDirectory, name, args);

        public async Task<string> GetCommandOutput(DirectoryInfo workingDirectory, string name, string args = "")
            => (await RunProcess(workingDirectory, name, args)).output;

        public async Task<TimeSpan> MeasureExecutionDuration(DirectoryInfo workingDirectory, string name, string args = "")
            => (await RunProcess(workingDirectory, name, args)).duration;

        private async Task<(TimeSpan duration, string output)> RunProcess(DirectoryInfo workingDirectory, string name, string args)
        {
            LogCommand(workingDirectory, name, args);

            var psi = new ProcessStartInfo(name, args)
            {
                WorkingDirectory = workingDirectory.FullName,
                RedirectStandardOutput = true
            };
            using Process p = Process.Start(psi) ?? throw new Exception("The process really should start.");
            DateTime startTime = p.StartTime;

            p.WaitForExit();

            if (p.ExitCode != 0)
            {
                throw new Exception($"'{workingDirectory.FullName}> {name} {args}' failed with error code {p.ExitCode}");
            }

            return (
                duration: p.ExitTime - startTime,
                output: await p.StandardOutput.ReadToEndAsync());
        }

        static void LogCommand(DirectoryInfo workingDirectory, string name, string args = "")
        {
            WriteSeparator();
            Console.WriteLine($"{workingDirectory.FullName}> {name} {args}");
            Console.WriteLine();
        }

        static void WriteSeparator()
        {
            Console.WriteLine();
            Console.WriteLine(">>>>>>>>>>>>>>>>>>>>>>>>>");
        }
    }
}
