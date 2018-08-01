using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    public interface IGitRepo
    {
        Task<string> GetFileContentsAsync(string filePath, string repoUri, string branch);

        Task CreateDarcBranchAsync(string repoUri, string branch);

        Task PushFilesAsync(Dictionary<string, GitCommit> filesToCommit, string repoUri, string pullRequestBaseBranch);

        Task<string> CheckForOpenPullRequestsAsync(string repoUri, string darcBranch);

        Task<string> CreatePullRequestAsync(string repoUri, string mergeWithBranch, string sourceBranch, string title = null, string description = null);

        Task<string> UpdatePullRequestAsync(string repoUri, string mergeWithBranch, string sourceBranch, int pullRequestId, string title = null, string description = null);

        Task<Dictionary<string, GitCommit>> GetCommitsForPathAsync(string repoUri, string sha, string branch, string path = "eng");

        Task GetCommitMapForPathAsync(string repoUri, string sha, string branch, Dictionary<string, GitCommit> commits, string path = "eng");

        Task<string> GetFileContentAsync(string ownerAndRepo, string path);

        Task<string> GetLastCommitShaAsync(string ownerAndRepo, string branch);

        Task<string> CheckIfFileExistsAsync(string repoUri, string filePath, string branch);

        HttpClient CreateHttpClient(string versionOverride = null);
    }
}
