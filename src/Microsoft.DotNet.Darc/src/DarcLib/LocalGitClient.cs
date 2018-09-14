// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.DarcLib
{
    class LocalGitClient : IGitRepo
    {
        string _gitRepoRoot;
        ILogger _logger;

        /// <summary>
        /// Construct a new local git client
        /// </summary>
        /// <param name="path">Current path</param>
        public LocalGitClient(string path, ILogger logger)
        {
            // TODO: Attempt to find the git repo root?
            _gitRepoRoot = path;
            _logger = logger;
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

        public Task<string> CreatePullRequestCommentAsync(string pullRequestUrl, string message)
        {
            throw new NotImplementedException();
        }

        public Task GetCommitMapForPathAsync(string repoUri, string branch, string assetsProducedInCommit, List<GitFile> files, string pullRequestBaseBranch, string path = "eng/common/")
        {
            throw new NotImplementedException();
        }

        public Task<List<GitFile>> GetCommitsForPathAsync(string repoUri, string branch, string assetsProducedInCommit, string pullRequestBaseBranch, string path = "eng/common/")
        {
            throw new NotImplementedException();
        }

        public Task<string> GetFileContentsAsync(string ownerAndRepo, string path)
        {
            return GetFileContentsAsync(path, null, null);
        }

        public Task<string> GetFileContentsAsync(string filePath, string repoUri, string branch)
        {
            System.IO.StreamReader streamReader = new System.IO.StreamReader(filePath);
            return streamReader.ReadToEndAsync();
        }

        public Task<List<GitFile>> GetFilesForCommitAsync(string repoUri, string commit, string path)
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

        public Task<IList<Commit>> GetPullRequestCommitsAsync(string pullRequestUrl)
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

        public Task PushFilesAsync(List<GitFile> filesToCommit, string repoUri, string branch, string commitMessage)
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

        public Task UpdatePullRequestCommentAsync(string pullRequestUrl, string commentId, string message)
        {
            throw new NotImplementedException();
        }
    }
}
