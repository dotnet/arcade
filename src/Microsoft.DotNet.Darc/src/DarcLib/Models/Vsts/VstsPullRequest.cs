namespace Microsoft.DotNet.Darc
{
    public class VstsPullRequest
    {
        public VstsPullRequest(string title, string description, string sourceBranch, string targetBranch)
        {
            Title = title;
            Description = description;
            SourceRefName = $"refs/heads/{sourceBranch}";
            TargetRefName = $"refs/heads/{targetBranch}";
        }

        public string Title { get; set; }

        public string Description { get; set; }

        public string SourceRefName { get; set; }

        public string TargetRefName { get; set; }
    }
}
