namespace Microsoft.DotNet.Darc
{
    public class GitHubRef
    {
        public GitHubRef(string githubRef, string sha)
        {
            Ref = githubRef;
            Sha = sha;
        }

        public string Ref { get; set; }

        public string Sha { get; set; }

        public bool Force { get; set; }
    }
}
