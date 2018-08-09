// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    public class GitHubClient : IGitRepo
    {
        private const string GitHubApiUri = "https://api.github.com";
        private const string DarcLibVersion = "1.0.0";
        private readonly string _userAgent = $"DarcLib-{DarcLibVersion}";
        private readonly string _personalAccessToken;
        private readonly ILogger _logger;
        private readonly JsonSerializerSettings _serializerSettings;

        public GitHubClient(string accessToken, ILogger logger)
        {
            _personalAccessToken = accessToken;
            _logger = logger;
            _serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        public async Task<string> GetFileContentsAsync(string filePath, string repoUri, string branch)
        {
            _logger.LogInformation($"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}'...");

            string ownerAndRepo = GetSegments(repoUri);

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repos/{ownerAndRepo}contents/{filePath}?ref={branch}", _logger);

            _logger.LogInformation($"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}' succeeded!");

            JObject responseContent = JObject.Parse(await response.Content.ReadAsStringAsync());

            string content = responseContent["content"].ToString();

            return this.GetDecodedContent(content);
        }

        public async Task CreateDarcBranchAsync(string repoUri, string branch)
        {
            _logger.LogInformation($"Verifying if 'darc-{branch}' branch exist in repo '{repoUri}'. If not, we'll create it...");

            string ownerAndRepo = GetSegments(repoUri);
            string latestSha = await GetLastCommitShaAsync(ownerAndRepo, branch);
            string body;

            GitHubRef githubRef = new GitHubRef($"refs/heads/darc-{branch}", latestSha);
            HttpResponseMessage response = null;

            try
            {
                response = await this.ExecuteGitCommand(HttpMethod.Get, $"repos/{ownerAndRepo}branches/darc-{branch}", _logger);
            }
            catch (HttpRequestException exc) when (exc.Message.Contains(((int)HttpStatusCode.NotFound).ToString()))
            {
                _logger.LogInformation($"'darc-{branch}' branch doesn't exist. Creating it...");

                body = JsonConvert.SerializeObject(githubRef, _serializerSettings);
                response = await this.ExecuteGitCommand(HttpMethod.Post, $"repos/{ownerAndRepo}git/refs", _logger, body);

                _logger.LogInformation($"Branch 'darc-{branch}' created in repo '{repoUri}'!");

                return;
            }
            catch (HttpRequestException exc)
            {
                _logger.LogError($"Checking if 'darc-{branch}' branch existed in repo '{repoUri}' failed with '{exc.Message}'");

                throw;
            }

            _logger.LogInformation($"Branch 'darc-{branch}' exists.");
        }

        public async Task PushFilesAsync(Dictionary<string, GitCommit> filesToCommit, string repoUri, string pullRequestBaseBranch)
        {
            _logger.LogInformation($"Pushing files to '{pullRequestBaseBranch}'...");

            string ownerAndRepo = GetSegments(repoUri);

            foreach (string filePath in filesToCommit.Keys)
            {
                GitCommit commit = filesToCommit[filePath] as GitCommit;
                string blobSha = await CheckIfFileExistsAsync(repoUri, filePath, pullRequestBaseBranch);

                if (!string.IsNullOrEmpty(blobSha))
                {
                    commit.Sha = blobSha;
                }

                string body = JsonConvert.SerializeObject(commit, _serializerSettings);

                await this.ExecuteGitCommand(HttpMethod.Put, $"repos/{ownerAndRepo}contents/{filePath}", _logger, body);

                _logger.LogInformation($"Pushing files to '{pullRequestBaseBranch}' succeeded!");
            }
        }

        public async Task<IEnumerable<int>> SearchPullRequestsAsync(string repoUri, string pullRequestBranch, PrStatus status, string keyword = null, string author = null)
        {
            string ownerAndRepo = GetSegments(repoUri);
            StringBuilder query = new StringBuilder();

            if (!string.IsNullOrEmpty(keyword))
            {
                query.Append(keyword);
                query.Append("+");
            }

            query.Append($"repo:{ownerAndRepo}+head:{pullRequestBranch}+type:pr+is:{status.ToString().ToLower()}");

            if (!string.IsNullOrEmpty(author))
            {
                query.Append($"+author:{author}");
            }

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"search/issues?q={query.ToString()}", _logger);

            JObject responseContent = JObject.Parse(await response.Content.ReadAsStringAsync());
            JArray items = JArray.Parse(responseContent["items"].ToString());

            IEnumerable<int> prs = items.Select(r => r["number"].ToObject<int>());

            return prs;
        }

        public async Task<PrStatus> GetPullRequestStatusAsync(string pullRequestUrl)
        {
            string url = GetSegments(pullRequestUrl).Replace("pull", "pulls");

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repos{url}", _logger);

            JObject responseContent = JObject.Parse(await response.Content.ReadAsStringAsync());

            if (Enum.TryParse(responseContent["state"].ToString(), true, out PrStatus status))
            {
                if (status == PrStatus.Open)
                {
                    return status;
                }

                if (status == PrStatus.Closed)
                {
                    if (bool.TryParse(responseContent["merged"].ToString(), out bool merged))
                    {
                        if (merged)
                        {
                            return PrStatus.Merged;
                        }
                    }

                    return PrStatus.Closed;
                }
            }

            return PrStatus.None;
        }

        public async Task<string> CreatePullRequestAsync(string repoUri, string mergeWithBranch, string sourceBranch, string title = null, string description = null)
        {
            string linkToPullRquest = await CreateOrUpdatePullRequestAsync(repoUri, mergeWithBranch, sourceBranch, HttpMethod.Post, title, description);
            return linkToPullRquest;
        }

        public async Task<string> UpdatePullRequestAsync(string pullRequestUri, string mergeWithBranch, string sourceBranch, string title = null, string description = null)
        {
            string linkToPullRquest = await CreateOrUpdatePullRequestAsync(pullRequestUri, mergeWithBranch, sourceBranch, new HttpMethod("PATCH"), title, description);
            return linkToPullRquest;
        }

        public async Task MergePullRequestAsync(string pullRequestUrl, string commit = null, string mergeMethod = GitHubMergeMethod.Merge, string title = null, string message = null)
        {
            title = title ?? PullRequestProperties.AutoMergeTitle;
            message = message ?? PullRequestProperties.AutoMergeMessage;

            GitHubPullRequestMerge pullRequestMerge = new GitHubPullRequestMerge(title, message, commit, mergeMethod);

            string body = JsonConvert.SerializeObject(pullRequestMerge, _serializerSettings);

            string url = GetSegments(pullRequestUrl).Replace("pulls", "pull");

            await this.ExecuteGitCommand(HttpMethod.Put, $"repos{url}/merge", _logger, body);
        }

        public async Task CommentOnPullRequestAsync(string repoUri, int pullRequestId, string message)
        {
            GitHubComment comment = new GitHubComment(message);

            string body = JsonConvert.SerializeObject(comment, _serializerSettings);

            string ownerAndRepo = GetSegments(repoUri);

            await this.ExecuteGitCommand(HttpMethod.Post, $"repos/{ownerAndRepo}issues/{pullRequestId}/comments", _logger, body);
        }

        public async Task<Dictionary<string, GitCommit>> GetCommitsForPathAsync(string repoUri, string branch, string assetsProducedInCommit, string pullRequestBaseBranch, string path = "eng")
        {
            Dictionary<string, GitCommit> commits = new Dictionary<string, GitCommit>();

            await GetCommitMapForPathAsync(repoUri, branch, assetsProducedInCommit, commits, pullRequestBaseBranch, path);

            return commits;
        }

        public async Task GetCommitMapForPathAsync(string repoUri, string branch, string assetsProducedInCommit, Dictionary<string, GitCommit> commits, string pullRequestBaseBranch, string path = "eng")
        {
            _logger.LogInformation($"Getting the contents of file/files in '{path}' of repo '{repoUri}' at commit '{assetsProducedInCommit}'");

            string ownerAndRepo = GetSegments(repoUri);
            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repos/{ownerAndRepo}contents/{path}?ref={assetsProducedInCommit}", _logger);

            List<GitHubContent> contents = JsonConvert.DeserializeObject<List<GitHubContent>>(await response.Content.ReadAsStringAsync());

            foreach (GitHubContent content in contents)
            {
                if (content.Type == GitHubContentType.File)
                {
                    if (!DependencyFileManager.DependencyFiles.Contains(content.Path))
                    {
                        string fileContent = await GetFileContentAsync(ownerAndRepo, content.Path);
                        GitCommit gitCommit = new GitCommit($"Updating contents of file '{content.Path}'", fileContent, pullRequestBaseBranch);
                        commits.Add(content.Path, gitCommit);
                    }
                }
                else
                {
                    await GetCommitMapForPathAsync(repoUri, branch, assetsProducedInCommit, commits, pullRequestBaseBranch, content.Path);
                }
            }

            _logger.LogInformation($"Getting the contents of file/files in '{path}' of repo '{repoUri}' at commit '{assetsProducedInCommit}' succeeded!");
        }

        public async Task<string> GetFileContentAsync(string ownerAndRepo, string path)
        {
            string encodedContent;

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repos/{ownerAndRepo}contents/{path}", _logger);

            JObject file = JObject.Parse(await response.Content.ReadAsStringAsync());
            encodedContent = file["content"].ToString();

            return encodedContent;
        }

        public HttpClient CreateHttpClient(string versionOverride = null)
        {
            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri(GitHubApiUri)
            };
            client.DefaultRequestHeaders.Add("Authorization", $"Token {_personalAccessToken}");
            client.DefaultRequestHeaders.Add("User-Agent", _userAgent);

            return client;
        }

        public async Task<string> CheckIfFileExistsAsync(string repoUri, string filePath, string branch)
        {
            string commit;
            string ownerAndRepo = GetSegments(repoUri);
            HttpResponseMessage response;

            try
            {
                response = await this.ExecuteGitCommand(HttpMethod.Get, $"repos/{ownerAndRepo}contents/{filePath}?ref={branch}", _logger);
            }
            catch (HttpRequestException exc)
            {
                if (exc.Message.Contains(((int)HttpStatusCode.NotFound).ToString()))
                {
                    return null;
                }

                throw exc;
            }

            JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());
            commit = content["sha"].ToString();

            return commit;
        }

        public async Task<string> GetLastCommitShaAsync(string ownerAndRepo, string branch)
        {
            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repos/{ownerAndRepo}commits/{branch}", _logger);

            JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());

            if (content == null)
            {
                throw new Exception($"No commits found in branch '{branch}' of repo '{ownerAndRepo}'!");
            }

            return content["sha"].ToString();
        }

        private async Task<string> GetUserNameAsync()
        {
            string user;

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, "user", _logger);

            JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());
            user = content["login"].ToString();

            return user;
        }

        private string GetSegments(string repoUri)
        {
            repoUri = repoUri.Replace("https://github.com/", string.Empty);
            repoUri = repoUri.Last() != '/' ? $"{repoUri}/" : repoUri;
            return repoUri;
        }

        private async Task<string> CreateOrUpdatePullRequestAsync(string uri, string mergeWithBranch, string sourceBranch, HttpMethod method, string title = null, string description = null)
        {
            string linkToPullRquest;
            string requestUri;
            string ownerAndRepo = GetSegments(uri);

            title = !string.IsNullOrEmpty(title) ? $"{PullRequestProperties.TitleTag} {title}" : PullRequestProperties.Title;
            description = description ?? PullRequestProperties.Description;

            GitHubPullRequest pullRequest = new GitHubPullRequest(title, description, sourceBranch, mergeWithBranch);

            string body = JsonConvert.SerializeObject(pullRequest, _serializerSettings);

            if (method == HttpMethod.Post)
            {
                requestUri = $"repos/{ownerAndRepo}pulls";
            }
            else
            {
                string url = ownerAndRepo.Replace("pull", "pulls");
                requestUri = $"repos{url}";
            }

            HttpResponseMessage response = await this.ExecuteGitCommand(method, requestUri, _logger, body);

            JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());
            linkToPullRquest = content["html_url"].ToString();

            return linkToPullRquest;
        }
    }
}
