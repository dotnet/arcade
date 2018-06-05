namespace Microsoft.DotNet.Darc
{
    public class GitHubPullRequest
    {
        public GitHubPullRequest(string title, string body, string head, string baseBranch)
        {
            Title = title;
            Body = body;
            Head = head;
            Base = baseBranch;
        }

        public string Title { get; set; }

        public string Body { get; set; }

        public string Head { get; set; }

        public string Base { get; set; }
    }
}
