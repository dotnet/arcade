// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.TestRunners.Common;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.NUnit;

/// <summary>
/// Interface to be implemented by the runner so that the listener can interact with it.
/// </summary>
internal interface INUnitTestRunner
{
    void IncreasePassedTests();

    void IncreaseSkippedTests();

    void IncreaseFailedTests();

    void IncreaseInconclusiveTests();

    void Add(TestFailureInfo info);

    bool GCAfterEachFixture { get; }

    string? TestsRootDirectory { get; }
}
