// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.XHarness.TestRunners.Common;

internal class TestExecutionState
{
    public string TestName { get; internal set; }
    public TimeSpan Started { get; private set; } = TimeSpan.MinValue;
    public TimeSpan Finished { get; private set; } = TimeSpan.MinValue;
    public TestCompletionStatus CompletionStatus { get; set; } = TestCompletionStatus.Undefined;

    internal TestExecutionState() { }

    internal void Start() => Started = new TimeSpan(DateTime.Now.Ticks);

    internal void Finish() => Finished = new TimeSpan(DateTime.Now.Ticks);
}
