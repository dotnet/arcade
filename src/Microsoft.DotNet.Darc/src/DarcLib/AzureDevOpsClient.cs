// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Microsoft.DotNet.DarcLib
{
    public class AzureDevOpsClient : IGitRepo
    {
        private const string DefaultApiVersion = "5.0-preview.1";

        private static readonly Regex repoUriPattern = new Regex(@"^/dnceng/(?<team>[^/]+)/_git/(?<repo>[^/]+)$");

        private static readonly Regex prUriPattern = new Regex(
            @"^/(?<team>[^/]+)/_apis/git/repositories/(?<repo>[^/])/pullRequests/(?<id>\d+)$");

        private readonly ILogger _logger;
        private readonly string _personalAccessToken;
        private readonly JsonSerializerSettings _serializerSettings;

        public AzureDevOpsClient(string accessToken, ILogger logger)
        {
            _personalAccessToken = accessToken;
            _logger = logger;
            _serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        private string AzureDevOpsApiUri { get; set; }

        private string AzureDevOpsPrUri { get; set; }

        public async Task<string> GetFileContentsAsync(string filePath, string repoUri, string branch)
        {
            _logger.LogInformation(
                $"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}'...");

            string repoName = SetApiUriAndGetRepoName(repoUri);

            HttpResponseMessage response = await this.ExecuteGitCommand(
                HttpMethod.Get,
                $"repositories/{repoName}/items?path={filePath}&version={branch}&includeContent=true",
                _logger);

            _logger.LogInformation(
                $"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}' succeeded!");

            JObject responseContent = JObject.Parse(await response.Content.ReadAsStringAsync());

            return responseContent["content"].ToString();
        }

        public async Task CreateBranchAsync(string repoUri, string newBranch, string baseBranch)
        {
            string repoName = SetApiUriAndGetRepoName(repoUri);
            string body;

            var azureDevOpsRefs = new List<AzureDevOpsRef>();
            AzureDevOpsRef azureDevOpsRef;
            HttpResponseMessage response = null;

            string latestSha = await GetLastCommitShaAsync(repoName, baseBranch);

            response = await this.ExecuteGitCommand(
                HttpMethod.Get,
                $"repositories/{repoName}/refs/heads/{newBranch}",
                _logger);
            JObject responseContent = JObject.Parse(await response.Content.ReadAsStringAsync());

            // Azure DevOps doesn't fail with a 404 if a branch does not exist, it just returns an empty response object...
            if (responseContent["count"].ToObject<int>() == 0)
            {
                _logger.LogInformation($"'{newBranch}' branch doesn't exist. Creating it...");

                azureDevOpsRef = new AzureDevOpsRef($"refs/heads/{newBranch}", latestSha);
                azureDevOpsRefs.Add(azureDevOpsRef);
            }
            else
            {
                _logger.LogInformation(
                    $"Branch '{newBranch}' exists, making sure it is in sync with '{baseBranch}'...");

                string oldSha = await GetLastCommitShaAsync(repoName, $"{newBranch}");

                azureDevOpsRef = new AzureDevOpsRef($"refs/heads/{newBranch}", latestSha, oldSha);
                azureDevOpsRefs.Add(azureDevOpsRef);
            }

            body = JsonConvert.SerializeObject(azureDevOpsRefs, _serializerSettings);
            await this.ExecuteGitCommand(HttpMethod.Post, $"repositories/{repoName}/refs", _logger, body);
        }

        public async Task PushFilesAsync(
            List<GitFile> filesToCommit,
            string repoUri,
            string branch,
            string commitMessage)
        {
            _logger.LogInformation($"Pushing files to '{branch}'...");

            var changes = new List<AzureDevOpsChange>();
            string repoName = SetApiUriAndGetRepoName(repoUri);

            foreach (GitFile gitfile in filesToCommit)
            {
                string blobSha = await CheckIfFileExistsAsync(repoUri, gitfile.FilePath, branch);

                var change = new AzureDevOpsChange(gitfile.FilePath, gitfile.Content);

                if (!string.IsNullOrEmpty(blobSha))
                {
                    change.ChangeType = AzureDevOpsChangeType.Edit;
                }

                changes.Add(change);
            }

            var commit = new AzureDevOpsCommit(changes, "Dependency files update");

            string latestSha = await GetLastCommitShaAsync(repoName, branch);
            var refUpdate = new AzureDevOpsRefUpdate($"refs/heads/{branch}", latestSha);

            var azureDevOpsPush = new AzureDevOpsPush(refUpdate, commit);

            string body = JsonConvert.SerializeObject(azureDevOpsPush, _serializerSettings);

            // Azure DevOps' contents API is only supported in version 5.0-preview.2
            await this.ExecuteGitCommand(
                HttpMethod.Post,
                $"repositories/{repoName}/pushes",
                _logger,
                body,
                "5.0-preview.2");

            _logger.LogInformation($"Pushing files to '{branch}' succeeded!");
        }

        public async Task<IEnumerable<int>> SearchPullRequestsAsync(
            string repoUri,
            string pullRequestBranch,
            PrStatus status,
            string keyword = null,
            string author = null)
        {
            string repoName = SetApiUriAndGetRepoName(repoUri);
            var query = new StringBuilder();
            AzureDevOpsPrStatus prStatus;

            switch (status)
            {
                case PrStatus.Open:
                    prStatus = AzureDevOpsPrStatus.Active;
                    break;
                case PrStatus.Closed:
                    prStatus = AzureDevOpsPrStatus.Abandoned;
                    break;
                case PrStatus.Merged:
                    prStatus = AzureDevOpsPrStatus.Completed;
                    break;
                default:
                    prStatus = AzureDevOpsPrStatus.None;
                    break;
            }

            query.Append(
                $"searchCriteria.sourceRefName=refs/heads/{pullRequestBranch}&searchCriteria.status={prStatus.ToString().ToLower()}");

            if (!string.IsNullOrEmpty(keyword))
            {
                _logger.LogInformation(
                    "A keyword was provided but Azure DevOps doesn't support searching for PRs based on keywords and it won't be used...");
            }

            if (!string.IsNullOrEmpty(author))
            {
                query.Append($"&searchCriteria.creatorId={author}");
            }

            HttpResponseMessage response = await this.ExecuteGitCommand(
                HttpMethod.Get,
                $"repositories/{repoName}/pullrequests?{query}",
                _logger);

            JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());
            JArray values = JArray.Parse(content["value"].ToString());

            IEnumerable<int> prs = values.Select(r => r["pullRequestId"].ToObject<int>());

            return prs;
        }

        public async Task<PrStatus> GetPullRequestStatusAsync(string pullRequestUrl)
        {
            string uri = GetPrPartialAbsolutePath(pullRequestUrl);

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, uri, _logger);

            JObject responseContent = JObject.Parse(await response.Content.ReadAsStringAsync());

            if (Enum.TryParse(responseContent["status"].ToString(), true, out AzureDevOpsPrStatus status))
            {
                if (status == AzureDevOpsPrStatus.Active)
                {
                    return PrStatus.Open;
                }

                if (status == AzureDevOpsPrStatus.Completed)
                {
                    return PrStatus.Merged;
                }

                if (status == AzureDevOpsPrStatus.Abandoned)
                {
                    return PrStatus.Closed;
                }
            }

            return PrStatus.None;
        }

        public async Task<string> GetPullRequestRepo(string pullRequestUrl)
        {
            string url = GetPrPartialAbsolutePath(pullRequestUrl);

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, url, _logger);

            JObject responseContent = JObject.Parse(await response.Content.ReadAsStringAsync());

            return responseContent["repository"]["remoteUrl"].ToString();
        }

        public async Task<PullRequest> GetPullRequestAsync(string pullRequestUrl)
        {
            VssConnection connection = CreateConnection(pullRequestUrl);
            GitHttpClient client = await connection.GetClientAsync<GitHttpClient>();

            (string team, string repo, int id) = ParsePullRequestUri(pullRequestUrl);

            GitPullRequest pr = await client.GetPullRequestAsync(team, repo, id);
            return new PullRequest
            {
                Title = pr.Title,
                Description = pr.Description,
                BaseBranch = pr.TargetRefName,
                HeadBranch = pr.SourceRefName
            };
        }

        public async Task<string> CreatePullRequestAsync(string repoUri, PullRequest pullRequest)
        {
            VssConnection connection = CreateConnection(repoUri);
            GitHttpClient client = await connection.GetClientAsync<GitHttpClient>();

            (string team, string repo) = ParseRepoUri(repoUri);

            GitPullRequest createdPr = await client.CreatePullRequestAsync(
                new GitPullRequest
                {
                    Title = pullRequest.Title,
                    Description = pullRequest.Description,
                    SourceRefName = "refs/heads/" + pullRequest.HeadBranch,
                    TargetRefName = "refs/heads/" + pullRequest.BaseBranch
                },
                team,
                repo);

            return createdPr.Url;
        }

        public async Task UpdatePullRequestAsync(string pullRequestUri, PullRequest pullRequest)
        {
            VssConnection connection = CreateConnection(pullRequestUri);
            GitHttpClient client = await connection.GetClientAsync<GitHttpClient>();

            (string team, string repo, int id) = ParsePullRequestUri(pullRequestUri);

            await client.UpdatePullRequestAsync(
                new GitPullRequest
                {
                    Title = pullRequest.Title,
                    Description = pullRequest.Description
                },
                team,
                repo,
                id);
        }

        public async Task MergePullRequestAsync(string pullRequestUrl, MergePullRequestParameters parameters)
        {
            VssConnection connection = CreateConnection(pullRequestUrl);
            GitHttpClient client = await connection.GetClientAsync<GitHttpClient>();

            (string team, string repo, int id) = ParsePullRequestUri(pullRequestUrl);

            await client.UpdatePullRequestAsync(
                new GitPullRequest
                {
                    Status = PullRequestStatus.Completed,
                    CompletionOptions = new GitPullRequestCompletionOptions
                    {
                        SquashMerge = parameters.SquashMerge,
                        DeleteSourceBranch = parameters.DeleteSourceBranch
                    },
                    LastMergeSourceCommit = new GitCommitRef {CommitId = parameters.CommitToMerge}
                },
                repo,
                id);
        }

        public Task CreateOrUpdatePullRequestDarcCommentAsync(string pullRequestUrl, string message)
        {
            throw new NotImplementedException();
        }

        public async Task<List<GitFile>> GetFilesForCommitAsync(string repoUri, string commit, string path)
        {
            var files = new List<GitFile>();

            await GetCommitMapForPathAsync(repoUri, commit, files, path);

            return files;
        }

        public async Task<string> GetFileContentsAsync(string ownerAndRepo, string path)
        {
            string encodedContent;

            HttpResponseMessage response = await this.ExecuteGitCommand(
                HttpMethod.Get,
                $"repositories/{ownerAndRepo}/items?path={path}&includeContent=true",
                _logger);

            JObject file = JObject.Parse(await response.Content.ReadAsStringAsync());
            encodedContent = file["content"].ToString();

            return encodedContent;
        }

        public async Task<string> GetLastCommitShaAsync(string ownerAndRepo, string branch)
        {
            HttpResponseMessage response = await this.ExecuteGitCommand(
                HttpMethod.Get,
                $"repositories/{ownerAndRepo}/commits?branch={branch}",
                _logger);

            JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());

            JArray values = JArray.Parse(content["value"].ToString());

            if (!values.Any())
            {
                throw new Exception($"No commits found in branch '{branch}' of '{ownerAndRepo}'");
            }

            return values[0]["commitId"].ToString();
        }

        public async Task<IList<Check>> GetPullRequestChecksAsync(string pullRequestUrl)
        {
            string url = $"{pullRequestUrl}/statuses";

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, url, _logger);

            JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());
            JArray values = JArray.Parse(content["value"].ToString());

            IList<Check> statuses = new List<Check>();
            foreach (JToken status in values)
            {
                if (Enum.TryParse(status["state"].ToString(), true, out AzureDevOpsCheckState state))
                {
                    CheckState checkState;

                    switch (state)
                    {
                        case AzureDevOpsCheckState.Error:
                            checkState = CheckState.Error;
                            break;
                        case AzureDevOpsCheckState.Failed:
                            checkState = CheckState.Failure;
                            break;
                        case AzureDevOpsCheckState.Pending:
                            checkState = CheckState.Pending;
                            break;
                        case AzureDevOpsCheckState.Succeeded:
                            checkState = CheckState.Success;
                            break;
                        default:
                            checkState = CheckState.None;
                            break;
                    }

                    statuses.Add(
                        new Check(
                            checkState,
                            status["context"]["name"].ToString(),
                            $"{pullRequestUrl}/{status["id"]}"));
                }
            }

            return statuses;
        }

        public async Task<string> GetPullRequestBaseBranch(string pullRequestUrl)
        {
            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, pullRequestUrl, _logger);

            JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());
            string baseBranch = content["sourceRefName"].ToString();
            const string refsHeads = "refs/heads/";
            if (baseBranch.StartsWith(refsHeads))
            {
                baseBranch = baseBranch.Substring(refsHeads.Length);
            }

            return baseBranch;
        }

        public async Task<IList<Commit>> GetPullRequestCommitsAsync(string pullRequestUrl)
        {
            VssConnection connection = CreateConnection(pullRequestUrl);
            GitHttpClient client = await connection.GetClientAsync<GitHttpClient>();

            (string team, string repo, int id) = ParsePullRequestUri(pullRequestUrl);

            List<GitCommitRef> commits = await client.GetPullRequestCommitsAsync(team, repo, id);

            return commits.Select(c => new Commit(c.Author.Name, c.CommitId)).ToList();
        }

        public HttpClient CreateHttpClient(string versionOverride = null)
        {
            var client = new HttpClient {BaseAddress = new Uri(AzureDevOpsApiUri)};
            client.DefaultRequestHeaders.Add(
                "Accept",
                $"application/json;api-version={versionOverride ?? DefaultApiVersion}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", _personalAccessToken))));

            return client;
        }

        public async Task<string> CheckIfFileExistsAsync(string repoUri, string filePath, string branch)
        {
            string repoName = SetApiUriAndGetRepoName(repoUri);
            HttpResponseMessage response;

            try
            {
                response = await this.ExecuteGitCommand(
                    HttpMethod.Get,
                    $"repositories/{repoName}/items?path={filePath}&versionDescriptor[version]={branch}",
                    _logger);
            }
            catch (HttpRequestException exc) when (exc.Message.Contains(((int) HttpStatusCode.NotFound).ToString()))
            {
                return null;
            }

            JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());

            return content["objectId"].ToString();
        }

        public string GetOwnerAndRepoFromRepoUri(string repoUri)
        {
            throw new NotImplementedException();
        }

        private VssConnection CreateConnection(string uri)
        {
            var collectionUri = new UriBuilder(uri)
            {
                Path = "",
                Query = "",
                Fragment = ""
            };
            var creds = new VssCredentials(new VssBasicCredential("", _personalAccessToken));
            return new VssConnection(collectionUri.Uri, creds);
        }

        private (string team, string repo) ParseRepoUri(string uri)
        {
            var u = new UriBuilder(uri);
            Match match = repoUriPattern.Match(u.Path);
            if (!match.Success)
            {
                return default;
            }

            return (match.Groups["team"].Value, match.Groups["repo"].Value);
        }

        private (string team, string repo, int id) ParsePullRequestUri(string uri)
        {
            var u = new UriBuilder(uri);
            Match match = prUriPattern.Match(u.Path);
            if (!match.Success)
            {
                return default;
            }

            return (match.Groups["team"].Value, match.Groups["repo"].Value, int.Parse(match.Groups["id"].Value));
        }

        public async Task<string> CreatePullRequestCommentAsync(string pullRequestUrl, string message)
        {
            VssConnection connection = CreateConnection(pullRequestUrl);
            GitHttpClient client = await connection.GetClientAsync<GitHttpClient>();

            (string team, string repo, int id) = ParsePullRequestUri(pullRequestUrl);

            GitPullRequestCommentThread thread = await client.CreateThreadAsync(
                new GitPullRequestCommentThread
                {
                    Comments = new List<Comment>
                    {
                        new Comment
                        {
                            CommentType = CommentType.Text,
                            Content = message
                        }
                    }
                },
                team,
                repo,
                id);

            return thread.Id + "-" + thread.Comments.First().Id;
        }

        public async Task UpdatePullRequestCommentAsync(string pullRequestUrl, string commentId, string message)
        {
            (int threadId, int commentIdValue) = ParseCommentId(commentId);

            VssConnection connection = CreateConnection(pullRequestUrl);
            GitHttpClient client = await connection.GetClientAsync<GitHttpClient>();

            (string team, string repo, int id) = ParsePullRequestUri(pullRequestUrl);

            await client.UpdateCommentAsync(
                new Comment
                {
                    CommentType = CommentType.Text,
                    Content = message
                },
                team,
                repo,
                id,
                threadId,
                commentIdValue);
        }

        private (int threadId, int commentId) ParseCommentId(string commentId)
        {
            string[] parts = commentId.Split('-');
            if (parts.Length != 2 || int.TryParse(parts[0], out int threadId) ||
                int.TryParse(parts[1], out int commentIdValue))
            {
                throw new ArgumentException("The comment id '{commentId}' is in an invalid format", nameof(commentId));
            }

            return (threadId, commentIdValue);
        }

        public async Task CommentOnPullRequestAsync(string pullRequestUrl, string message)
        {
            SetApiUriAndGetRepoName(pullRequestUrl);
            var comments = new List<AzureDevOpsCommentBody> {new AzureDevOpsCommentBody(message)};

            var comment = new AzureDevOpsComment(comments);

            string body = JsonConvert.SerializeObject(comment, _serializerSettings);

            await this.ExecuteGitCommand(HttpMethod.Post, $"{pullRequestUrl}/threads", _logger, body);
        }

        public async Task GetCommitMapForPathAsync(string repoUri, string commit, List<GitFile> files, string path)
        {
            _logger.LogInformation(
                $"Getting the contents of file/files in '{path}' of repo '{repoUri}' at commit '{commit}'");

            string repoName = SetApiUriAndGetRepoName(repoUri);

            HttpResponseMessage response = await this.ExecuteGitCommand(
                HttpMethod.Get,
                $"repositories/{repoName}/items?scopePath={path}&version={commit}&includeContent=true&versionType=commit&recursionLevel=full",
                _logger);

            JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());
            var items = JsonConvert.DeserializeObject<List<AzureDevOpsItem>>(Convert.ToString(content["value"]));

            foreach (AzureDevOpsItem item in items)
            {
                if (!item.IsFolder)
                {
                    if (!GitFileManager.DependencyFiles.Contains(item.Path))
                    {
                        string fileContent = await GetFileContentsAsync(repoName, item.Path);
                        var gitCommit = new GitFile(item.Path, fileContent);
                        files.Add(gitCommit);
                    }
                }
            }

            _logger.LogInformation(
                $"Getting the contents of file/files in '{path}' of repo '{repoUri}' at commit '{commit}' succeeded!");
        }

        private string SetApiUriAndGetRepoName(string repoUri)
        {
            var uri = new Uri(repoUri);
            string hostName = uri.Host;
            string accountName;
            string projectName;
            string repoName;

            Match hostNameMatch = Regex.Match(hostName, @"^(?<accountname>[a-z0-9]+)\.*");

            if (hostNameMatch.Success)
            {
                accountName = hostNameMatch.Groups["accountname"].Value;
            }
            else
            {
                throw new ArgumentException(
                    $"Repository URI host name '{hostName}' should be of the form dev.azure.com/<accountname> i.e. https://dev.azure.com/<accountname>");
            }

            string absolutePath = uri.AbsolutePath;
            Match projectAndRepoMatch = Regex.Match(
                absolutePath,
                @"[\/DefaultCollection]*\/(?<projectname>.+)\/_git\/(?<reponame>.+)");

            if (projectAndRepoMatch.Success)
            {
                projectName = projectAndRepoMatch.Groups["projectname"].Value;
                repoName = projectAndRepoMatch.Groups["reponame"].Value;
            }
            else
            {
                throw new ArgumentException(
                    $"Repository URI host name '{absolutePath}' should have a project and repo name. i.e. /DefaultCollection/<projectname>/_git/<reponame>");
            }

            AzureDevOpsApiUri = $"https://dev.azure.com/{accountName}/{projectName}/_apis/git/";
            AzureDevOpsPrUri = $"https://dev.azure.com/{accountName}/{projectName}/_git/{repoName}/pullrequest/";

            return repoName;
        }

        private async Task<string> CreateOrUpdatePullRequestAsync(
            string uri,
            string mergeWithBranch,
            string sourceBranch,
            HttpMethod method,
            string title = null,
            string description = null)
        {
            string requestUri;
            string repoName = SetApiUriAndGetRepoName(uri);

            title = !string.IsNullOrEmpty(title)
                ? $"{PullRequestProperties.TitleTag} {title}"
                : PullRequestProperties.Title;
            description = description ?? PullRequestProperties.Description;

            var pullRequest = new AzureDevOpsPullRequest(title, description, sourceBranch, mergeWithBranch);

            string body = JsonConvert.SerializeObject(pullRequest, _serializerSettings);

            if (method == HttpMethod.Post)
            {
                requestUri = $"repositories/{repoName}/pullrequests";
            }
            else
            {
                requestUri = GetPrPartialAbsolutePath(uri);
            }

            HttpResponseMessage response = await this.ExecuteGitCommand(method, requestUri, _logger, body);

            JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());

            Console.WriteLine($"Browser ready link for this PR is: '{AzureDevOpsPrUri}{content["pullRequestId"]}'");

            return content["url"].ToString();
        }

        private string GetPrPartialAbsolutePath(string prLink)
        {
            var uri = new Uri(prLink);
            string toRemove = $"{uri.Host}/_apis/git/";
            return prLink.Replace(toRemove, string.Empty);
        }
    }
}
