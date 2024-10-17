namespace Microsoft.DotNet.XHarness.TestRunners.Common;

/// <summary>
/// Enumeration used to state the result of a test.
/// </summary>
public enum TestResult
{
    /// <summary>
    /// Test was executed and passed.
    /// </summary>
    Passed,
    /// <summary>
    /// Test was executed and failed.
    /// </summary>
    Failed,
    /// <summary>
    /// Test was not executed but was skipped.
    /// </summary>
    Skipped,
}
