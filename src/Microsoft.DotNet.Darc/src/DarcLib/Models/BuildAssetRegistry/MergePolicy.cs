namespace Microsoft.DotNet.DarcLib
{
    public enum MergePolicy
    {
        None = 0,
        BuildSucceeded,
        UnitTestPassed,
        Never
    }
}
