using System.Collections.Generic;
using NUnit.Engine;
using NUnit.Framework.Interfaces;

namespace Microsoft.DotNet.XHarness.TestRunners.NUnit;

public interface IResultSummary : IList<ITestRun>
{
    string Name { get; }
    string FullName { get; }
    long InconclusiveTests { get; }
    long FailedTests { get; }
    long PassedTests { get; }
    long SkippedTests { get; }
    long ExecutedTests { get; }
    long TotalTests { get; }
    long FilteredTests { get; }
    long AssertCount { get; }
    double Duration { get; }
    TestStatus TestStatus { get; }
}
