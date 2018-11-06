// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.DarcLib.Helpers;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.DarcLib
{
    public class LocalGitClient : IGitRepo
    {
        private ILogger _logger;

        /// <summary>
        ///     Construct a new local git client
        /// </summary>
        /// <param name="path">Current path</param>
        public LocalGitClient(ILogger logger)
        {
            _logger = logger;
        }

        public Task<string> CheckIfFileExistsAsync(string repoUri, string filePath, string branch)
        {
            throw new NotImplementedException();
        }

        public Task CreateBranchAsync(string repoUri, string newBranch, string baseBranch)
        {
            throw new InvalidOperationException();
        }

        public HttpClient CreateHttpClient(string versionOverride = null)
        {
            throw new InvalidOperationException();
        }

        public Task<string> GetFileContentsAsync(string ownerAndRepo, string path)
        {
            return GetFileContentsAsync(path, null, null);
        }

        public async Task<string> GetFileContentsAsync(string relativeFilePath, string repoUri, string branch)
        {
            string fullPath = Path.Combine(repoUri, relativeFilePath);
            using (var streamReader = new StreamReader(fullPath))
            {
                return await streamReader.ReadToEndAsync();
            }
        }

        public Task CreateOrUpdatePullRequestDarcCommentAsync(string pullRequestUrl, string message)
        {
            throw new NotImplementedException();
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

        public Task<PullRequest> GetPullRequestAsync(string pullRequestUrl)
        {
            throw new NotImplementedException();
        }

        public Task<string> CreatePullRequestAsync(string repoUri, PullRequest pullRequest)
        {
            throw new NotImplementedException();
        }

        public Task UpdatePullRequestAsync(string pullRequestUri, PullRequest pullRequest)
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

        /// <summary>
        ///     Updates local copies of the files.
        /// </summary>
        /// <param name="filesToCommit">Files to update locally</param>
        /// <param name="repoUri">Base path of the repo</param>
        /// <param name="branch">Unused</param>
        /// <param name="commitMessage">Unused</param>
        /// <returns></returns>
        public async Task PushFilesAsync(List<GitFile> filesToCommit, string repoUri, string branch, string commitMessage)
        {
            foreach (GitFile file in filesToCommit)
            {
                string fullPath = Path.Combine(repoUri, file.FilePath);
                using (var streamWriter = new StreamWriter(fullPath))
                {
                    string finalContent;
                    switch (file.ContentEncoding)
                    {
                        case "utf-8":
                            finalContent = file.Content;
                            break;
                        case "base64":
                            byte[] bytes = Convert.FromBase64String(file.Content);
                            finalContent = Encoding.UTF8.GetString(bytes);
                            break;
                        default:
                            throw new DarcException($"Unknown file content encoding {file.ContentEncoding}");
                    }
                    finalContent = NormalizeLineEndings(fullPath, finalContent);
                    await streamWriter.WriteAsync(finalContent);
                }
            }
        }

        /// <summary>
        /// Normalize line endings of content.
        /// </summary>
        /// <param name="filePath">Path of file</param>
        /// <param name="content">Content to normalize</param>
        /// <returns>Normalized content</returns>
        /// <remarks>
        ///     Normalize based on the following rules:
        ///     - Auto CRLF is assumed.
        ///     - Check the git attributes the file to determine whether it has a specific setting for the file.  If so, use that.
        ///     - If no setting, or if auto, then determine whether incoming content differs in line ends vs. the
        ///       OS setting, and replace if needed.
        /// </remarks>
        private string NormalizeLineEndings(string filePath, string content)
        {
            const string crlf = "\r\n";
            const string lf = "\n";
            // Check gitAttributes to determine whether the file has eof handling set.
            string eofAttr = LocalHelpers.ExecuteCommand("git", $"check-attr eol -- {filePath}", _logger);
            if (string.IsNullOrEmpty(eofAttr) ||
                eofAttr.Contains("eol: unspecified") ||
                eofAttr.Contains("eol: auto"))
            {
                if (Environment.NewLine != crlf)
                {
                    return content.Replace(crlf, Environment.NewLine);
                }
                else if (Environment.NewLine == crlf && !content.Contains(crlf))
                {
                    return content.Replace(lf, Environment.NewLine);
                }
            }
            else if (eofAttr.Contains("eol: crlf"))
            {
                // Test to avoid adding extra \r.
                if (!content.Contains(crlf))
                {
                    return content.Replace(lf, crlf);
                }
            }
            else if (eofAttr.Contains("eol: lf"))
            {
                return content.Replace(crlf, lf);
            }
            else
            {
                throw new DarcException($"Unknown eof setting '{eofAttr}' for file '{filePath};");
            }
            return content;
        }

        public string GetOwnerAndRepoFromRepoUri(string repoUri)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<int>> SearchPullRequestsAsync(
            string repoUri,
            string pullRequestBranch,
            PrStatus status,
            string keyword = null,
            string author = null)
        {
            throw new NotImplementedException();
        }
    }
}
