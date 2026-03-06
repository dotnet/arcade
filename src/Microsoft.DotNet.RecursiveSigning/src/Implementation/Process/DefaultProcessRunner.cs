// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    /// <summary>
    /// Default process runner that shells out via <see cref="System.Diagnostics.Process"/>.
    /// </summary>
    public sealed class DefaultProcessRunner : IProcessRunner
    {
        public async Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken)
        {
            using var proc = new System.Diagnostics.Process();
            proc.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            proc.Start();

            // Read stdout and stderr concurrently to avoid deadlocks
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();

            await proc.WaitForExitAsync(cancellationToken);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return new ProcessResult(proc.ExitCode, stdout, stderr);
        }
    }
}
