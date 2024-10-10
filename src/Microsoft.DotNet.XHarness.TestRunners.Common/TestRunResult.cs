namespace Microsoft.DotNet.XHarness.TestRunners.Common;

public struct TestRunResult
{
    /// <summary>
    /// Retrieve the number of executed tests in a run.
    /// </summary>
    public long ExecutedTests { get; private set; }

    /// <summary>
    /// Retrieve the number of failed tests in a run.
    /// </summary>
    public long FailedTests { get; private set; }

    /// <summary>
    /// Retrieve the number of not executed tests due to the filters in a
    /// run.
    /// </summary>
    public long FilteredTests { get; private set; }

    /// <summary>
    /// Retrieve the number of inconclusive tests in a run.
    /// </summary>
    public long InconclusiveTests { get; private set; }

    /// <summary>
    /// Retrieve the number of passed tests in a run.
    /// </summary>
    public long PassedTests { get; private set; }

    /// <summary>
    /// Retrieve the number of skipped tests in a run.
    /// </summary>
    public long SkippedTests { get; private set; }

    /// <summary>
    /// Retrieve the total number of tests in a run. This value
    /// includes all skipped and filtered tests and might no be equal
    /// to the value returned by ExecutedTests.
    /// </summary>
    public long TotalTests { get; private set; }

    internal TestRunResult(TestRunner runner)
    {
        ExecutedTests = runner.ExecutedTests;
        FailedTests = runner.FailedTests;
        FilteredTests = runner.FilteredTests;
        InconclusiveTests = runner.InconclusiveTests;
        PassedTests = runner.PassedTests;
        SkippedTests = runner.SkippedTests;
        TotalTests = runner.TotalTests;
    }
}
