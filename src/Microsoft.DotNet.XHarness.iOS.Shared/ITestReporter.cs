// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.XHarness.Common.Execution;
using Microsoft.DotNet.XHarness.Common.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared;

/// <summary>
/// Interface that represents a class that knows how to parse test results.
/// </summary>
public interface ITestReporter : IDisposable
{
    ILog CallbackLog { get; }
    bool? Success { get; }
    CancellationToken CancellationToken { get; }

    void LaunchCallback(Task<bool> launchResult);
    Task CollectSimulatorResult(ProcessExecutionResult runResult);
    Task CollectDeviceResult(ProcessExecutionResult runResult);
    Task<(TestExecutingResult ExecutingResult, string ResultMessage)> ParseResult();
}
