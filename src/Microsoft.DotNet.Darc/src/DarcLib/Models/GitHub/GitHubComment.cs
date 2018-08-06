namespace Microsoft.DotNet.DarcLib
{
    public class GitHubComment
    {
        public GitHubComment(string commentBody)
        {
            Body = commentBody;
        }

        public string Body { get; set; }
    }
}
