using Newtonsoft.Json;

namespace Microsoft.DotNet.DarcLib
{
    public class GitHubPullRequestMerge
    {
        public GitHubPullRequestMerge(string title, string message, string sha, string method)
        {
            Title = title;
            Message = message;
            Sha = sha;
            Method = method;
        }

        [JsonProperty("commit_title")]
        public string Title { get; set; }

        [JsonProperty("commit_message")]
        public string Message { get; set; }

        public string Sha { get; set; }

        [JsonProperty("merge_method")]
        public string Method { get; set; }
    }
}
