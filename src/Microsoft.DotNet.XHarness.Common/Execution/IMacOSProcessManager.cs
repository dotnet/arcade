// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Logging;

namespace Microsoft.DotNet.XHarness.Common.Execution;

public interface IMacOSProcessManager : IProcessManager
{
    string XcodeRoot { get; }

    public Version XcodeVersion { get; }

    Task<ProcessExecutionResult> ExecuteXcodeCommandAsync(
        string executable,
        IList<string> args,
        ILog log,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    Task<ProcessExecutionResult> ExecuteXcodeCommandAsync(
        string executable,
        IList<string> args,
        ILog log,
        ILog stdoutLog,
        ILog stderrLog,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
