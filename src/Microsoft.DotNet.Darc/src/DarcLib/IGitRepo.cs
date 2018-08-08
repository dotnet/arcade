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

        Task CreateDarcBranchAsync(string repoUri, string branch);

        Task PushFilesAsync(Dictionary<string, GitCommit> filesToCommit, string repoUri, string pullRequestBaseBranch);

        Task<IEnumerable<int>> SearchPullRequestsAsync(string repoUri, string pullRequestBranch, PrStatus status, string keyword = null, string author = null);

        Task<PrStatus> GetPullRequestStatusAsync(string pullRequestUrl);

        Task<string> CreatePullRequestAsync(string repoUri, string mergeWithBranch, string sourceBranch, string title = null, string description = null);

        Task<string> UpdatePullRequestAsync(string repoUri, string mergeWithBranch, string sourceBranch, int pullRequestId, string title = null, string description = null);

        Task MergePullRequestAsync(string pullRequestUrl, string commit = null, string mergeMethod = null, string title = null, string message = null);

        Task CommentOnPullRequestAsync(string repoUri, int pullRequestId, string message);

        Task<Dictionary<string, GitCommit>> GetCommitsForPathAsync(string repoUri, string branch, string assetsProducedInCommit, string pullRequestBaseBranch, string path = "eng");

        Task GetCommitMapForPathAsync(string repoUri, string branch, string assetsProducedInCommit, Dictionary<string, GitCommit> commits, string pullRequestBaseBranch, string path = "eng");

        Task<string> GetFileContentAsync(string ownerAndRepo, string path);

        Task<string> GetLastCommitShaAsync(string ownerAndRepo, string branch);

        Task<string> CheckIfFileExistsAsync(string repoUri, string filePath, string branch);

        HttpClient CreateHttpClient(string versionOverride = null);
    }
}
