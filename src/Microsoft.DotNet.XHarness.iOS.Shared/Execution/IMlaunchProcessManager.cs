// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.Execution;

public interface IMlaunchProcessManager : IMacOSProcessManager, IProcessManager
{
    string MlaunchPath { get; }

    Task<ProcessExecutionResult> ExecuteCommandAsync(
        MlaunchArguments args,
        ILog log,
        TimeSpan timeout,
        Dictionary<string, string>? environmentVariables = null,
        int verbosity = 5,
        CancellationToken? cancellationToken = null);

    Task<ProcessExecutionResult> ExecuteCommandAsync(
        MlaunchArguments args,
        ILog log,
        ILog stdoutLog,
        ILog stderrLog,
        TimeSpan timeout,
        Dictionary<string, string>? environmentVariables = null,
        int verbosity = 5,
        CancellationToken? cancellationToken = null);

    Task<ProcessExecutionResult> RunAsync(
        Process process,
        MlaunchArguments args,
        ILog log,
        TimeSpan? timeout = null,
        Dictionary<string, string>? environmentVariables = null,
        int verbosity = 5,
        CancellationToken? cancellationToken = null,
        bool? diagnostics = null);

    Task<ProcessExecutionResult> RunAsync(
        Process process,
        MlaunchArguments args,
        ILog log,
        ILog stdoutLog,
        ILog stderrLog,
        TimeSpan? timeout = null,
        Dictionary<string, string>? environmentVariables = null,
        int verbosity = 5,
        CancellationToken? cancellationToken = null,
        bool? diagnostics = null);
}
