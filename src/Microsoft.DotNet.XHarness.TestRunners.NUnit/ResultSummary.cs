// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.XHarness.TestRunners.Common;
using NUnit.Engine;
using NUnit.Framework.Interfaces;

namespace Microsoft.DotNet.XHarness.TestRunners.NUnit;

/// <summary>
/// Helper class used to summarize the result of a test run.
/// </summary>
internal class ResultSummary : List<ITestRun>, IResultSummary
{
    private readonly TestRunner _runner;
    private double? _duration;
    private long? _assertCount;

    public string Name { get; private set; }
    public string FullName => Name;

    public long InconclusiveTests => _runner.InconclusiveTests;
    public long FailedTests => _runner.FilteredTests;
    public long PassedTests => _runner.PassedTests;
    public long SkippedTests => _runner.SkippedTests;
    public long ExecutedTests => _runner.ExecutedTests;
    public long TotalTests => _runner.TotalTests;
    public long FilteredTests => _runner.FilteredTests;

    public long AssertCount
    {
        get
        {
            if (_assertCount.HasValue)
            {
                return _assertCount.Value;
            }
            // not super efficient
            GetSummaryData();
            return _assertCount!.Value;
        }
    }

    public double Duration
    {
        get
        {
            if (_duration.HasValue)
            {
                return _duration.Value;
            }
            // not very efficient, but we should not cate too much
            GetSummaryData();
            return _duration!.Value;
        }
    }

    private void GetSummaryData()
    {
        double duration = 0;
        long assertCount = 0;
        foreach (var result in this)
        {
            var testRunNode = result.Result.FirstChild;
            if (testRunNode.Name != "test-run")
            {
                continue;
            }

            if (double.TryParse(testRunNode.Attributes["time"].Value, out var time))
            {
                duration += time;
            }
            if (long.TryParse(testRunNode.Attributes["asserts"].Value, out var asserts))
            {
                assertCount += asserts;
            }
        }

        _duration = duration;
        _assertCount = assertCount;
    }

    public TestStatus TestStatus
    {
        get
        {
            return FailedTests > 0 ? TestStatus.Failed : TestStatus.Passed;
        }
    }

    public ResultSummary(string testSuite, TestRunner testRunner) : base()
    {
        Name = testSuite ?? throw new ArgumentNullException(nameof(testSuite));
        _runner = testRunner ?? throw new ArgumentNullException(nameof(testRunner));
    }

}
