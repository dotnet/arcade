namespace Microsoft.DotNet.DarcLib
{
    public class VstsPullRequestMerge
    {
        public VstsPullRequestMerge(string message, string commitId, bool squash = false)
        {
            CompletionOptions = new CompletionOptions(message, squash);
            LastMergeSourceCommit = new LastMergeSourceCommit(commitId);
        }

        public int Status { get; } = 3;

        public CompletionOptions CompletionOptions { get; set; }

        public LastMergeSourceCommit LastMergeSourceCommit { get; set; }
    }

    public class CompletionOptions
    {
        public CompletionOptions(string message, bool squash)
        {
            MergeCommitMessage = message;
            SquashMerge = squash;
        }

        public string MergeCommitMessage { get; internal set; }

        public bool SquashMerge { get; internal set; }
    }

    public class LastMergeSourceCommit
    {
        public LastMergeSourceCommit(string commit)
        {
            CommitId = commit;
        }

        public string CommitId { get; internal set; }
    }
}
