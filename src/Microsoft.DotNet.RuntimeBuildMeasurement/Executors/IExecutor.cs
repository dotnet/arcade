using System;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.RuntimeBuildMeasurement.Executors
{
    public interface IExecutor
    {
        Task Execute(DirectoryInfo workingDirectory, string name, string args = "");

        Task<string> GetCommandOutput(DirectoryInfo workingDirectory, string name, string args = "");

        Task<TimeSpan> MeasureExecutionDuration(DirectoryInfo workingDirectory, string name, string args = "");
    }

    public static class ExecutorMixin
    {
        public static Task<string> Git(this IExecutor executor, DirectoryInfo workingDirectory, string args)
            => executor.GetCommandOutput(workingDirectory, "git", args);

        public static Task ExecuteRepoCommand(this IExecutor executor, DirectoryInfo workingDirectory, string name, string args = "")
            => executor.Execute(workingDirectory, Path.Combine(workingDirectory.FullName, name), args);

        public static Task<TimeSpan> MeasureRepoCommandDuration(this IExecutor executor, DirectoryInfo workingDirectory, string name, string args = "")
            => executor.MeasureExecutionDuration(workingDirectory, Path.Combine(workingDirectory.FullName, name), args);
    }
}
