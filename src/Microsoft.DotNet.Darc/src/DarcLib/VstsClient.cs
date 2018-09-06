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
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace Microsoft.DotNet.DarcLib
{
    public class VstsClient : IGitRepo
    {
        private const string DefaultApiVersion = "5.0-preview.1";
        private readonly string _personalAccessToken;
        private readonly ILogger _logger;
        private readonly JsonSerializerSettings _serializerSettings;

        private string VstsApiUri { get; set; }

        private string VstsPrUri { get; set; }

        public VstsClient(string accessToken, ILogger logger)
        {
            _personalAccessToken = accessToken;
            _logger = logger;
            _serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };
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

        public async Task<string> GetFileContentsAsync(string filePath, string repoUri, string branch)
        {
            _logger.LogInformation($"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}'...");

            string repoName = SetApiUriAndGetRepoName(repoUri);

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repositories/{repoName}/items?path={filePath}&version={branch}&includeContent=true", _logger);

            _logger.LogInformation($"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}' succeeded!");

            JObject responseContent = JObject.Parse(await response.Content.ReadAsStringAsync());

            return responseContent["content"].ToString();
        }

        public async Task CreateBranchAsync(string repoUri, string newBranch, string baseBranch)
        {
            string repoName = SetApiUriAndGetRepoName(repoUri);
            string body;

            List<VstsRef> vstsRefs = new List<VstsRef>();
            VstsRef vstsRef;
            HttpResponseMessage response = null;

            string latestSha = await GetLastCommitShaAsync(repoName, baseBranch);

            response = await this.ExecuteGitCommand(HttpMethod.Get, $"repositories/{repoName}/refs/heads/{newBranch}", _logger);
            JObject responseContent = JObject.Parse(await response.Content.ReadAsStringAsync());

            // VSTS doesn't fail with a 404 if a branch does not exist, it just returns an empty response object...
            if (responseContent["count"].ToObject<int>() == 0)
            {
                _logger.LogInformation($"'{newBranch}' branch doesn't exist. Creating it...");

                vstsRef = new VstsRef($"refs/heads/{newBranch}", latestSha);
                vstsRefs.Add(vstsRef);
            }
            else
            {
                _logger.LogInformation($"Branch '{newBranch}' exists, making sure it is in sync with '{baseBranch}'...");

                string oldSha = await GetLastCommitShaAsync(repoName, $"{newBranch}");

                vstsRef = new VstsRef($"refs/heads/{newBranch}", latestSha, oldSha);
                vstsRefs.Add(vstsRef);
            }

            body = JsonConvert.SerializeObject(vstsRefs, _serializerSettings);
            await this.ExecuteGitCommand(HttpMethod.Post, $"repositories/{repoName}/refs", _logger, body);
        }

        public async Task PushCommitsAsync(List<GitFile> filesToCommit, string repoUri, string pullRequestBaseBranch, string commitMessage)
        {
            _logger.LogInformation($"Pushing files to '{pullRequestBaseBranch}'...");

            List<VstsChange> changes = new List<VstsChange>();
            string repoName = SetApiUriAndGetRepoName(repoUri);

            foreach (GitFile gitfile in filesToCommit)
            {
                string blobSha = await CheckIfFileExistsAsync(repoUri, gitfile.FilePath, pullRequestBaseBranch);

                VstsChange change = new VstsChange(gitfile.FilePath, gitfile.Content);

                if (!string.IsNullOrEmpty(blobSha))
                {
                    change.ChangeType = VstsChangeType.Edit;
                }

                changes.Add(change);
            }

            VstsCommit commit = new VstsCommit(changes, "Dependency files update");

            string latestSha = await GetLastCommitShaAsync(repoName, pullRequestBaseBranch);
            VstsRefUpdate refUpdate = new VstsRefUpdate($"refs/heads/{pullRequestBaseBranch}", latestSha);

            VstsPush vstsPush = new VstsPush(refUpdate, commit);

            string body = JsonConvert.SerializeObject(vstsPush, _serializerSettings);

            // VSTS' contents API is only supported in version 5.0-preview.2
            await this.ExecuteGitCommand(HttpMethod.Post, $"repositories/{repoName}/pushes", _logger, body, "5.0-preview.2");

            _logger.LogInformation($"Pushing files to '{pullRequestBaseBranch}' succeeded!");
        }

        public async Task<IEnumerable<int>> SearchPullRequestsAsync(string repoUri, string pullRequestBranch, PrStatus status, string keyword = null, string author = null)
        {
            string repoName = SetApiUriAndGetRepoName(repoUri);
            StringBuilder query = new StringBuilder();
            VstsPrStatus prStatus;

            switch (status)
            {
                case PrStatus.Open:
                    prStatus = VstsPrStatus.Active;
                    break;
                case PrStatus.Closed:
                    prStatus = VstsPrStatus.Abandoned;
                    break;
                case PrStatus.Merged:
                    prStatus = VstsPrStatus.Completed;
                    break;
                default:
                    prStatus = VstsPrStatus.None;
                    break;
            }

            query.Append($"searchCriteria.sourceRefName=refs/heads/{pullRequestBranch}&searchCriteria.status={prStatus.ToString().ToLower()}");

            if (!string.IsNullOrEmpty(keyword))
            {
                _logger.LogInformation("A keyword was provided but VSTS doesn't support searching for PRs based on keywords and it won't be used...");
            }

            if (!string.IsNullOrEmpty(author))
            {
                query.Append($"&searchCriteria.creatorId={author}");
            }

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repositories/{repoName}/pullrequests?{query.ToString()}", _logger);

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

            if (Enum.TryParse(responseContent["status"].ToString(), true, out VstsPrStatus status))
            {
                if (status == VstsPrStatus.Active)
                {
                    return PrStatus.Open;
                }

                if (status == VstsPrStatus.Completed)
                {
                    return PrStatus.Merged;
                }

                if (status == VstsPrStatus.Abandoned)
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

        public async Task<string> CreatePullRequestAsync(string repoUri, string mergeWithBranch, string sourceBranch, string title = null, string description = null)
        {
            string linkToPullRquest = await CreateOrUpdatePullRequestAsync(repoUri, mergeWithBranch, sourceBranch, HttpMethod.Post,title, description);
            return linkToPullRquest;
        }

        public async Task<string> UpdatePullRequestAsync(string pullRequestUri, string mergeWithBranch, string sourceBranch, string title = null, string description = null)
        {
            string linkToPullRquest = await CreateOrUpdatePullRequestAsync(pullRequestUri, mergeWithBranch, sourceBranch, new HttpMethod("PATCH"), title, description);
            return linkToPullRquest;
        }

        public async Task MergePullRequestAsync(string pullRequestUrl, MergePullRequestParameters parameters)
        {
            var connection = CreateConnection(pullRequestUrl);
            var client = await connection.GetClientAsync<GitHttpClient>();

            var (team, repo, id) = ParsePullRequestUri(pullRequestUrl);

            await client.UpdatePullRequestAsync(
                new GitPullRequest
                {
                    Status = PullRequestStatus.Completed,
                    CompletionOptions = new GitPullRequestCompletionOptions
                    {
                        SquashMerge = parameters.SquashMerge,
                        DeleteSourceBranch = parameters.DeleteSourceBranch,
                    },
                    LastMergeSourceCommit = new GitCommitRef
                    {
                        CommitId = parameters.CommitToMerge
                    },
                },
                repo,
                id);
        }

        private static Regex prUriPattern = new Regex(@"^/(?<team>[^/])/_apis/git/repositories/(?<repo>[^/])/pullRequests/(?<id>\d+)$");

        private (string team, string repo, int id) ParsePullRequestUri(string uri)
        {
            var u = new UriBuilder(uri);
            var match = prUriPattern.Match(u.Path);
            if (!match.Success)
            {
                return default;
            }

            return (match.Groups["team"].Value, match.Groups["repo"].Value, int.Parse(match.Groups["id"].Value));
        }

        public async Task CommentOnPullRequestAsync(string pullRequestUrl, string message)
        {
            SetApiUriAndGetRepoName(pullRequestUrl);
            List<VstsCommentBody> comments = new List<VstsCommentBody>
            {
                new VstsCommentBody(message)
            };

            VstsComment comment = new VstsComment(comments);

            string body = JsonConvert.SerializeObject(comment, _serializerSettings);

            await this.ExecuteGitCommand(HttpMethod.Post, $"{pullRequestUrl}/threads", _logger, body);
        }

        public async Task<List<GitFile>> GetCommitsForPathAsync(string repoUri, string branch, string assetsProducedInCommit, string pullRequestBaseBranch, string path = "eng/common/")
        {
            List<GitFile> files = new List<GitFile>();

            await GetCommitMapForPathAsync(repoUri, branch, assetsProducedInCommit, files, pullRequestBaseBranch, path);

            return files;
        }

        public async Task GetCommitMapForPathAsync(string repoUri, string branch, string assetsProducedInCommit, List<GitFile> files, string pullRequestBaseBranch, string path = "eng/common/")
        {
            _logger.LogInformation($"Getting the contents of file/files in '{path}' of repo '{repoUri}' at commit '{assetsProducedInCommit}'");

            string repoName = SetApiUriAndGetRepoName(repoUri);

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repositories/{repoName}/items?scopePath={path}&version={assetsProducedInCommit}&includeContent=true&versionType=commit&recursionLevel=full", _logger);

            JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());
            List<VstsItem> items = JsonConvert.DeserializeObject<List<VstsItem>>(Convert.ToString(content["value"]));

            foreach (VstsItem item in items)
            {
                if (!item.IsFolder)
                {
                    if (!GitFileManager.DependencyFiles.Contains(item.Path))
                    {
                        string fileContent = await GetFileContentAsync(repoName, item.Path);
                        GitFile gitCommit = new GitFile(item.Path, fileContent);
                        files.Add(gitCommit);
                    }
                }
            }

            _logger.LogInformation($"Getting the contents of file/files in '{path}' of repo '{repoUri}' at commit '{assetsProducedInCommit}' succeeded!");
        }

        public async Task<string> GetFileContentAsync(string ownerAndRepo, string path)
        {
            string encodedContent;

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repositories/{ownerAndRepo}/items?path={path}&includeContent=true", _logger);

            JObject file = JObject.Parse(await response.Content.ReadAsStringAsync());
            encodedContent = file["content"].ToString();

            return encodedContent;
        }

        public async Task<string> GetLastCommitShaAsync(string ownerAndRepo, string branch)
        {
            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repositories/{ownerAndRepo}/commits?branch={branch}", _logger);

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
                if (Enum.TryParse(status["state"].ToString(), true, out VstsCheckState state))
                {
                    CheckState checkState;

                    switch (state)
                    {
                        case VstsCheckState.Error:
                            checkState = CheckState.Error;
                            break;
                        case VstsCheckState.Failed:
                            checkState = CheckState.Failure;
                            break;
                        case VstsCheckState.Pending:
                            checkState = CheckState.Pending;
                            break;
                        case VstsCheckState.Succeeded:
                            checkState = CheckState.Success;
                            break;
                        default:
                            checkState = CheckState.None;
                            break;
                    }

                    statuses.Add(new Check(checkState, status["context"]["name"].ToString(), $"{pullRequestUrl}/{status["id"]}".ToString()));
                }
            }

            return statuses;
        }

        public async Task<string> GetPullRequestBaseBranch(string pullRequestUrl)
        {
            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, pullRequestUrl, _logger);

            JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());
            var baseBranch = content["sourceRefName"].ToString();
            const string refsHeads = "refs/heads/";
            if (baseBranch.StartsWith(refsHeads))
            {
                baseBranch = baseBranch.Substring(refsHeads.Length);
            }
            return baseBranch;
        }

        public HttpClient CreateHttpClient(string versionOverride = null)
        {
            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri(VstsApiUri)
            };
            client.DefaultRequestHeaders.Add("Accept", $"application/json;api-version={versionOverride ?? DefaultApiVersion}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", _personalAccessToken))));

            return client;
        }

        public async Task<string> CheckIfFileExistsAsync(string repoUri, string filePath, string branch)
        {
            string repoName = SetApiUriAndGetRepoName(repoUri);
            HttpResponseMessage response;

            try
            {
                response = await this.ExecuteGitCommand(HttpMethod.Get, $"repositories/{repoName}/items?path={filePath}&versionDescriptor[version]={branch}", _logger);
            }
            catch (HttpRequestException exc) when (exc.Message.Contains(((int)HttpStatusCode.NotFound).ToString()))
            {
                return null;
            }

            JObject content = JObject.Parse(await response.Content.ReadAsStringAsync());

            return content["objectId"].ToString();
        }

        private string SetApiUriAndGetRepoName(string repoUri)
        {
            Uri uri = new Uri(repoUri);
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
                throw new ArgumentException($"Repository URI host name '{hostName}' should be of the form <accountname>.visualstudio.com i.e. https://<accountname>.visualstudio.com");
            }

            string absolutePath = uri.AbsolutePath;
            Match projectAndRepoMatch = Regex.Match(absolutePath, @"[\/DefaultCollection]*\/(?<projectname>.+)\/_git\/(?<reponame>.+)");

            if (projectAndRepoMatch.Success)
            {
                projectName = projectAndRepoMatch.Groups["projectname"].Value;
                repoName = projectAndRepoMatch.Groups["reponame"].Value;
            }
            else
            {
                throw new ArgumentException($"Repository URI host name '{absolutePath}' should have a project and repo name. i.e. /DefaultCollection/<projectname>/_git/<reponame>");
            }

            VstsApiUri = $"https://{accountName}.visualstudio.com/{projectName}/_apis/git/";
            VstsPrUri = $"https://{accountName}.visualstudio.com/{projectName}/_git/{repoName}/pullrequest/";

            return repoName;
        }

        private async Task<string> CreateOrUpdatePullRequestAsync(string uri, string mergeWithBranch, string sourceBranch, HttpMethod method, string title = null, string description = null)
        {
            string requestUri;
            string repoName = SetApiUriAndGetRepoName(uri);

            title = !string.IsNullOrEmpty(title) ? $"{PullRequestProperties.TitleTag} {title}" : PullRequestProperties.Title;
            description = description ?? PullRequestProperties.Description;

            VstsPullRequest pullRequest = new VstsPullRequest(title, description, sourceBranch, mergeWithBranch);

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

            Console.WriteLine($"Browser ready link for this PR is: '{VstsPrUri}{content["pullRequestId"].ToString()}'");

            return content["url"].ToString();
        }

        private string GetPrPartialAbsolutePath(string prLink)
        {
            Uri uri = new Uri(prLink);
            string toRemove = $"{uri.Host}/_apis/git/";
            return prLink.Replace(toRemove, string.Empty);
        }
    }
}
