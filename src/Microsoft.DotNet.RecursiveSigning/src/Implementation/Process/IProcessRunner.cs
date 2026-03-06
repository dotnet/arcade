// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    /// <summary>
    /// Abstraction for running external processes. Enables testability.
    /// </summary>
    public interface IProcessRunner
    {
        /// <summary>
        /// Runs a process and returns the result after it exits.
        /// </summary>
        Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Result of a process execution.
    /// </summary>
    public sealed class ProcessResult
    {
        public int ExitCode { get; }
        public string StandardOutput { get; }
        public string StandardError { get; }

        public ProcessResult(int exitCode, string standardOutput, string standardError)
        {
            ExitCode = exitCode;
            StandardOutput = standardOutput ?? string.Empty;
            StandardError = standardError ?? string.Empty;
        }
    }
}
