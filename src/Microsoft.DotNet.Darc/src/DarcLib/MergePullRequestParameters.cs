namespace Microsoft.DotNet.DarcLib
{
    public class MergePullRequestParameters
    {
        public string CommitToMerge { get; set; }
        public bool SquashMerge { get; set; } = true;
        public bool DeleteSourceBranch { get; set; } = true;
    }
}
