// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    public interface IGitRepo
    {
        Task<string> GetFileContentsAsync(string filePath, string repoUri, string branch);

        Task CreateBranchAsync(string repoUri, string newBranch, string baseBranch);

        Task PushCommitsAsync(List<GitFile> filesToCommit, string repoUri, string pullRequestBaseBranch, string commitMessage);

        Task<IEnumerable<int>> SearchPullRequestsAsync(string repoUri, string pullRequestBranch, PrStatus status, string keyword = null, string author = null);

        Task<PrStatus> GetPullRequestStatusAsync(string pullRequestUrl);

        Task<string> GetPullRequestRepo(string pullRequestUrl);

        Task<string> CreatePullRequestAsync(string repoUri, string mergeWithBranch, string sourceBranch, string title = null, string description = null);

        Task<string> UpdatePullRequestAsync(string pullRequestUri, string mergeWithBranch, string sourceBranch, string title = null, string description = null);

        Task MergePullRequestAsync(string pullRequestUrl, MergePullRequestParameters parameters);

        Task CommentOnPullRequestAsync(string pullRequestUrl, string message);

        Task<List<GitFile>> GetCommitsForPathAsync(string repoUri, string branch, string assetsProducedInCommit, string pullRequestBaseBranch, string path = "eng/common/");

        Task GetCommitMapForPathAsync(string repoUri, string branch, string assetsProducedInCommit, List<GitFile> files, string pullRequestBaseBranch, string path = "eng/common/");

        Task<string> GetFileContentAsync(string ownerAndRepo, string path);

        Task<string> GetLastCommitShaAsync(string ownerAndRepo, string branch);

        Task<string> CheckIfFileExistsAsync(string repoUri, string filePath, string branch);

        HttpClient CreateHttpClient(string versionOverride = null);

        Task<IList<Check>> GetPullRequestChecksAsync(string pullRequestUrl);

        Task<string> GetPullRequestBaseBranch(string pullRequestUrl);
    }
}
