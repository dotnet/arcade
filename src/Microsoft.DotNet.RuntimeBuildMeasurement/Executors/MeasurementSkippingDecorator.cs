using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.RuntimeBuildMeasurement.Executors
{
    public class MeasurementSkippingDecorator : IExecutor
    {
        private readonly IExecutor _executor;

        public MeasurementSkippingDecorator(IExecutor executor)
        {
            _executor = executor;
        }

        public Task Execute(DirectoryInfo workingDirectory, string name, string args = "")
            => _executor.Execute(workingDirectory, name, args);

        public Task<string> GetCommandOutput(DirectoryInfo workingDirectory, string name, string args = "")
            => _executor.GetCommandOutput(workingDirectory, name, args);

        public Task<TimeSpan> MeasureExecutionDuration(DirectoryInfo workingDirectory, string name, string args = "")
        {
            Console.WriteLine($"# {workingDirectory.FullName}> {name} {args}");
            Console.WriteLine();

            return Task.FromResult(TimeSpan.Zero);
        }
    }
}
