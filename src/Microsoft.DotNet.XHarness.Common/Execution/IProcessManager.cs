// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;

namespace Microsoft.DotNet.XHarness.Common.Execution;

public class ProcessExecutionResult
{
    public bool TimedOut { get; set; }
    public int ExitCode { get; set; }
    public bool Succeeded => !TimedOut && ExitCode == 0;
}

/// <summary>
/// Interface that helps to manage processes
/// </summary>
public interface IProcessManager
{
    Task<ProcessExecutionResult> ExecuteCommandAsync(
        string filename,
        IList<string> args,
        ILog log,
        TimeSpan timeout,
        Dictionary<string, string>? environmentVariables = null,
        CancellationToken? cancellationToken = null);

    Task<ProcessExecutionResult> ExecuteCommandAsync(
        string filename,
        IList<string> args,
        ILog log,
        ILog stdoutLog,
        ILog stderrLog,
        TimeSpan timeout,
        Dictionary<string, string>? environmentVariables = null,
        CancellationToken? cancellationToken = null);

    Task<ProcessExecutionResult> RunAsync(
        Process process,
        ILog log,
        TimeSpan? timeout = null,
        Dictionary<string, string>? environmentVariables = null,
        CancellationToken? cancellationToken = null,
        bool? diagnostics = null);

    Task<ProcessExecutionResult> RunAsync(
        Process process,
        ILog log,
        ILog stdoutLog,
        ILog stderrLog,
        TimeSpan? timeout = null,
        Dictionary<string, string>? environmentVariables = null,
        CancellationToken? cancellationToken = null,
        bool? diagnostics = null);

    Task KillTreeAsync(Process process, ILog log, bool? diagnostics = true);
    Task KillTreeAsync(int pid, ILog log, bool? diagnostics = true);
}
