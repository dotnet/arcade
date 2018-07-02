using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc
{
    public class GitHubClient : IGitRepo
    {
        private const string GitHubApiUri = "https://api.github.com";
        private readonly string personalAccessToken;
        private readonly ILogger _logger;

        public GitHubClient(string accessToken, ILogger logger)
        {
            personalAccessToken = accessToken;
            _logger = logger;
        }

        public async Task<string> GetFileContentsAsync(string filePath, string repoUri, string branch)
        {
            _logger.LogInformation($"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}'...");

            string ownerAndRepo = GetOwnerAndRepo(repoUri);

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repos/{ownerAndRepo}contents/{filePath}?ref={branch}", _logger);

            _logger.LogInformation($"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}' succeeded!");

            dynamic responseContent = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());

            string content = Convert.ToString(responseContent.content);

            return this.GetDecodedContent(content);
        }

        public async Task CreateDarcBranchAsync(string repoUri, string branch)
        {
            _logger.LogInformation($"Verifying if 'darc-{branch}' branch exist in repo '{repoUri}'. If not, we'll create it...");

            string ownerAndRepo = GetOwnerAndRepo(repoUri);
            string latestSha = await GetLastCommitShaAsync(ownerAndRepo, branch);
            string body;

            GitHubRef githubRef = new GitHubRef($"refs/heads/darc-{branch}", latestSha);
            HttpResponseMessage response = null;

            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            try
            {
                response = await this.ExecuteGitCommand(HttpMethod.Get, $"repos/{ownerAndRepo}branches/darc-{branch}", _logger);
            }
            catch (HttpRequestException exc)
            {
                if (exc.Message.Contains(((int)HttpStatusCode.NotFound).ToString()))
                {
                    _logger.LogInformation($"'darc-{branch}' branch doesn't exist. Creating it...");

                    body = JsonConvert.SerializeObject(githubRef, serializerSettings);
                    response = await this.ExecuteGitCommand(HttpMethod.Post, $"repos/{ownerAndRepo}git/refs", _logger, body);

                    _logger.LogInformation($"Branch 'darc-{branch}' created in repo '{repoUri}'!");

                    return;
                }
                else
                {
                    _logger.LogError($"Checking if 'darc-{branch}' branch existed in repo '{repoUri}' failed with '{exc.Message}'");

                    throw;
                }
            }

            _logger.LogInformation($"Branch 'darc-{branch}' exists, making sure it is in sync with '{branch}'...");

            githubRef.Force = true;
            body = JsonConvert.SerializeObject(githubRef, serializerSettings);
            response = await this.ExecuteGitCommand(new HttpMethod("PATCH"), $"repos/{ownerAndRepo}git/{githubRef.Ref}", _logger, body);

            _logger.LogInformation($"Branch 'darc-{branch}' now in sync with'{branch}'.");
        }

        public async Task PushFilesAsync(Dictionary<string, GitCommit> filesToCommit, string repoUri, string pullRequestBaseBranch)
        {
            _logger.LogInformation($"Pushing files to '{pullRequestBaseBranch}'...");

            string ownerAndRepo = GetOwnerAndRepo(repoUri);

            foreach (string filePath in filesToCommit.Keys)
            {
                GitCommit commit = filesToCommit[filePath] as GitCommit;
                string blobSha = await CheckIfFileExistsAsync(repoUri, filePath, pullRequestBaseBranch);

                if (!string.IsNullOrEmpty(blobSha))
                {
                    commit.Sha = blobSha;
                }

                JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                };

                string body = JsonConvert.SerializeObject(commit, serializerSettings);

                await this.ExecuteGitCommand(HttpMethod.Put, $"repos/{ownerAndRepo}contents/{filePath}", _logger, body);

                _logger.LogInformation($"Pushing files to '{pullRequestBaseBranch}' succeeded!");
            }
        }

        public async Task<string> CheckForOpenPullRequestsAsync(string repoUri, string darcBranch)
        {
            string pullRequestLink = null;
            string ownerAndRepo = GetOwnerAndRepo(repoUri);
            string user = await GetUserNameAsync();

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repos/{ownerAndRepo}pulls?head={user}:{darcBranch}", _logger);

            List<dynamic> content = JsonConvert.DeserializeObject<List<dynamic>>(await response.Content.ReadAsStringAsync());
            dynamic pr = content.Where(p => ((string)p.title).Contains("[Darc-Update]")).FirstOrDefault();

            if (pr != null)
            {
                pullRequestLink = pr.html_url;
            }

            return pullRequestLink;
        }

        public async Task<string> CreatePullRequestAsync(string repoUri, string mergeWithBranch, string sourceBranch, string title = null, string description = null)
        {
            _logger.LogInformation($"Creating pull request from '{sourceBranch}' to '{mergeWithBranch}'...");

            string linkToPullRquest = await CreateOrUpdatePullRequestAsync(repoUri, mergeWithBranch, sourceBranch, HttpMethod.Post, 0, title, description);

            _logger.LogInformation($"Creating pull request from '{sourceBranch}' to '{mergeWithBranch}' succeeded. Link to the PR is: {linkToPullRquest}");

            return linkToPullRquest;
        }

        public async Task<string> UpdatePullRequestAsync(string repoUri, string mergeWithBranch, string sourceBranch, int pullRequestId, string title = null, string description = null)
        {
            _logger.LogInformation($"Updating pull request with id '{pullRequestId}' from '{sourceBranch}' to '{mergeWithBranch}'...");

            string linkToPullRquest = await CreateOrUpdatePullRequestAsync(repoUri, mergeWithBranch, sourceBranch, new HttpMethod("PATCH"), pullRequestId, title, description);

            _logger.LogInformation($"Updating pull request from '{sourceBranch}' to '{mergeWithBranch}' succeeded. Link to the PR is: {linkToPullRquest}");

            return linkToPullRquest;
        }

        public async Task<Dictionary<string, GitCommit>> GetCommitsForPathAsync(string repoUri, string sha, string branch, string path = "eng")
        {
            Dictionary<string, GitCommit> commits = new Dictionary<string, GitCommit>();

            await GetCommitMapForPathAsync(repoUri, sha, branch, commits, path);

            return commits;
        }

        public async Task GetCommitMapForPathAsync(string repoUri, string sha, string branch, Dictionary<string, GitCommit> commits, string path = "eng")
        {
            _logger.LogInformation($"Getting the contents of file/files in '{path}' of repo '{repoUri}' in sha '{sha}'");

            string ownerAndRepo = GetOwnerAndRepo(repoUri);
            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repos/{ownerAndRepo}contents/{path}?ref={sha}", _logger);

            List<GitHubContent> contents = JsonConvert.DeserializeObject<List<GitHubContent>>(await response.Content.ReadAsStringAsync());

            foreach (GitHubContent content in contents)
            {
                if (content.Type == GitHubContentType.File)
                {
                    if (!DependencyFileManager.DependencyFiles.Contains(content.Path))
                    {
                        string fileContent = await GetFileContentAsync(ownerAndRepo, content.Path);
                        GitCommit commit = new GitCommit($"Updating contents of file '{content.Path}'", fileContent, branch);
                        commits.Add(content.Path, commit);
                    }
                }
                else
                {
                    await GetCommitMapForPathAsync(repoUri, sha, branch, commits, content.Path);
                }
            }

            _logger.LogInformation($"Getting the contents of file/files in '{path}' of repo '{repoUri}' at sha '{sha}' succeeded!");
        }

        public async Task<string> GetFileContentAsync(string ownerAndRepo, string path)
        {
            string encodedContent;

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repos/{ownerAndRepo}contents/{path}", _logger);

            dynamic file = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
            encodedContent = file.content;

            return encodedContent;
        }

        public HttpClient CreateHttpClient(string versionOverride = null)
        {
            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri(GitHubApiUri)
            };
            client.DefaultRequestHeaders.Add("Authorization", $"Token {personalAccessToken}");
            client.DefaultRequestHeaders.Add("User-Agent", "DarcLib");

            return client;
        }

        public async Task<string> CheckIfFileExistsAsync(string repoUri, string filePath, string branch)
        {
            string sha;
            string ownerAndRepo = GetOwnerAndRepo(repoUri);
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

            dynamic content = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
            sha = content.sha;

            return sha;
        }

        public async Task<string> GetLastCommitShaAsync(string ownerAndRepo, string branch)
        {
            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repos/{ownerAndRepo}commits/{branch}", _logger);

            dynamic content = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());

            if (content == null)
            {
                throw new Exception($"No commits found in branch '{branch}' of repo '{ownerAndRepo}'!");
            }

            return content.sha;
        }

        private async Task<string> GetUserNameAsync()
        {
            string user;

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, "user", _logger);

            dynamic content = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
            user = content.login;

            return user;
        }

        private string GetOwnerAndRepo(string repoUri)
        {
            repoUri = repoUri.Replace("https://github.com/", string.Empty);
            repoUri = repoUri.Last() != '/' ? $"{repoUri}/" : repoUri;
            return repoUri;
        }

        private async Task<string> CreateOrUpdatePullRequestAsync(string repoUri, string mergeWithBranch, string sourceBranch, HttpMethod method, int pullRequestId = 0, string title = null, string description = null)
        {
            string linkToPullRquest;
            string requestUri;
            string ownerAndRepo = GetOwnerAndRepo(repoUri);

            title = !string.IsNullOrEmpty(title) ? $"{PullRequestProperties.TitleTag} {title}" : PullRequestProperties.Title;
            description = description ?? PullRequestProperties.Description;

            GitHubPullRequest pullRequest = new GitHubPullRequest(title, description, sourceBranch, mergeWithBranch);
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            string body = JsonConvert.SerializeObject(pullRequest, serializerSettings);

            if (method == HttpMethod.Post)
            {
                requestUri = $"repos/{ownerAndRepo}pulls";
            }
            else
            {
                requestUri = $"repos/{ownerAndRepo}pulls/{pullRequestId}";
            }

            HttpResponseMessage response = await this.ExecuteGitCommand(method, requestUri, _logger, body);

            dynamic content = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
            linkToPullRquest = content.html_url;

            return linkToPullRquest;
        }
    }
}
