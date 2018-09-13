// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    class LocalGitClient : IGitRepo
    {
        string _gitRepoRoot;
        /// <summary>
        /// Construct a new local git client
        /// </summary>
        /// <param name="path">Current path</param>
        public LocalGitClient(string path)
        {
            // TODO: Attempt to find the git repo root?
            _gitRepoRoot = path;
        }

        public Task<string> CheckIfFileExistsAsync(string repoUri, string filePath, string branch)
        {
            throw new NotImplementedException();
        }

        public Task CommentOnPullRequestAsync(string pullRequestUrl, string message)
        {
            throw new InvalidOperationException();
        }

        public Task CreateBranchAsync(string repoUri, string newBranch, string baseBranch)
        {
            throw new InvalidOperationException();
        }

        public HttpClient CreateHttpClient(string versionOverride = null)
        {
            throw new InvalidOperationException();
        }

        public Task<string> CreatePullRequestAsync(string repoUri, string mergeWithBranch, string sourceBranch, string title = null, string description = null)
        {
            throw new InvalidOperationException();
        }

        public Task GetCommitMapForPathAsync(string repoUri, string branch, string assetsProducedInCommit, List<GitFile> files, string pullRequestBaseBranch, string path = "eng/common/")
        {
            throw new NotImplementedException();
        }

        public Task<List<GitFile>> GetCommitsForPathAsync(string repoUri, string branch, string assetsProducedInCommit, string pullRequestBaseBranch, string path = "eng/common/")
        {
            throw new NotImplementedException();
        }

        public Task<string> GetFileContentAsync(string ownerAndRepo, string path)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetFileContentsAsync(string filePath, string repoUri, string branch)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetLastCommitShaAsync(string ownerAndRepo, string branch)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetPullRequestBaseBranch(string pullRequestUrl)
        {
            throw new NotImplementedException();
        }

        public Task<IList<Check>> GetPullRequestChecksAsync(string pullRequestUrl)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetPullRequestRepo(string pullRequestUrl)
        {
            throw new NotImplementedException();
        }

        public Task<PrStatus> GetPullRequestStatusAsync(string pullRequestUrl)
        {
            throw new NotImplementedException();
        }

        public Task MergePullRequestAsync(string pullRequestUrl, MergePullRequestParameters parameters)
        {
            throw new NotImplementedException();
        }

        public Task PushCommitsAsync(List<GitFile> filesToCommit, string repoUri, string pullRequestBaseBranch, string commitMessage)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<int>> SearchPullRequestsAsync(string repoUri, string pullRequestBranch, PrStatus status, string keyword = null, string author = null)
        {
            throw new NotImplementedException();
        }

        public Task<string> UpdatePullRequestAsync(string pullRequestUri, string mergeWithBranch, string sourceBranch, string title = null, string description = null)
        {
            throw new NotImplementedException();
        }
    }
}
