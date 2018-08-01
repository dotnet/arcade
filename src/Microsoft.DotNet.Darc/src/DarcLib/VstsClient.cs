// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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

namespace Microsoft.DotNet.DarcLib
{
    public class VstsClient : IGitRepo
    {
        private const string DefaultApiVersion = "5.0-preview.1";
        private readonly string personalAccessToken;
        private readonly ILogger _logger;

        private string VstsApiUri { get; set; }

        private string VstsAccountName { get; set; }

        private string VstsProjectName { get; set; }

        private string VstsPrUri { get; set; }

        public VstsClient(string accessToken, ILogger logger)
        {
            personalAccessToken = accessToken;
            _logger = logger;
        }

        public async Task<string> GetFileContentsAsync(string filePath, string repoUri, string branch)
        {
            _logger.LogInformation($"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}'...");

            string repoName = SetApiUriAndGetRepoName(repoUri);

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repositories/{repoName}/items?path={filePath}&version={branch}&includeContent=true", _logger);

            _logger.LogInformation($"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}' succeeded!");

            dynamic responseContent = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());

            return responseContent.content;
        }

        public async Task CreateDarcBranchAsync(string repoUri, string branch)
        {
            _logger.LogInformation($"Verifying if 'darc-{branch}' branch exist in repo '{repoUri}'. If not, we'll create it...");

            string repoName = SetApiUriAndGetRepoName(repoUri);
            string body;
            string operation;

            List<VstsRef> vstsRefs = new List<VstsRef>();
            VstsRef vstsRef;
            HttpResponseMessage response = null;

            string latestSha = await GetLastCommitShaAsync(repoName, branch);

            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            response = await this.ExecuteGitCommand(HttpMethod.Get, $"repositories/{repoName}/refs/heads/darc-{branch}", _logger);
            dynamic responseContent = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());

            // VSTS doesn't fail with a 404 if a branch does not exist, it just returns an empty response object...
            if (responseContent.count == 0)
            {
                _logger.LogInformation($"'darc-{branch}' branch doesn't exist. Creating it...");

                vstsRef = new VstsRef($"refs/heads/darc-{branch}", latestSha);
                vstsRefs.Add(vstsRef);
                operation = "created in";
            }
            else
            {
                _logger.LogInformation($"Branch 'darc-{branch}' exists, making sure it is in sync with '{branch}'...");

                string oldSha = await GetLastCommitShaAsync(repoName, $"darc-{branch}");

                vstsRef = new VstsRef($"refs/heads/darc-{branch}", latestSha, oldSha);
                vstsRefs.Add(vstsRef);
                operation = "in sync with";
            }

            body = JsonConvert.SerializeObject(vstsRefs, serializerSettings);
            await this.ExecuteGitCommand(HttpMethod.Post, $"repositories/{repoName}/refs", _logger, body);

            _logger.LogInformation($"Branch 'darc-{branch}' {operation} repo '{repoUri}'!");

            return;
        }

        public async Task PushFilesAsync(Dictionary<string, GitCommit> filesToCommit, string repoUri, string pullRequestBaseBranch)
        {
            _logger.LogInformation($"Pushing files to '{pullRequestBaseBranch}'...");

            List<VstsChange> changes = new List<VstsChange>();
            string repoName = SetApiUriAndGetRepoName(repoUri);

            foreach (string filePath in filesToCommit.Keys)
            {
                string content = this.GetDecodedContent(filesToCommit[filePath].Content);
                string blobSha = await CheckIfFileExistsAsync(repoUri, filePath, pullRequestBaseBranch);

                VstsChange change = new VstsChange(filePath, content);

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

            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };

            string body = JsonConvert.SerializeObject(vstsPush, serializerSettings);

            // VSTS' contents API is only supported in version 5.0-preview.2
            await this.ExecuteGitCommand(HttpMethod.Post, $"repositories/{repoName}/pushes", _logger, body, "5.0-preview.2");

            _logger.LogInformation($"Pushing files to '{pullRequestBaseBranch}' succeeded!");
        }

        public async Task<string> CheckForOpenPullRequestsAsync(string repoUri, string darcBranch)
        {
            string pullRequestLink = null;
            string repoName = SetApiUriAndGetRepoName(repoUri);

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repositories/{repoName}/pullrequests?searchCriteria.targetRefName={darcBranch}", _logger);

            dynamic content = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
            List<dynamic> values = JsonConvert.DeserializeObject<List<dynamic>>(Convert.ToString(content.value));

            dynamic pr = values.Where(p => ((string)p.title).Contains(PullRequestProperties.TitleTag)).FirstOrDefault();

            if (pr != null)
            {
                pullRequestLink = $"{VstsPrUri}{pr.pullRequestId}";
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

        public async Task<Dictionary<string, GitCommit>> GetCommitsForPathAsync(string repoUri, string branch, string assetsProducedInCommit, string path = "eng")
        {
            Dictionary<string, GitCommit> commits = new Dictionary<string, GitCommit>();

            await GetCommitMapForPathAsync(repoUri, branch, assetsProducedInCommit, commits, path);

            return commits;
        }

        public async Task GetCommitMapForPathAsync(string repoUri, string branch, string assetsProducedInCommit, Dictionary<string, GitCommit> commits, string path = "eng")
        {
            _logger.LogInformation($"Getting the contents of file/files in '{path}' of repo '{repoUri}' at commit '{assetsProducedInCommit}'");

            string repoName = SetApiUriAndGetRepoName(repoUri);

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repositories/{repoName}/items?scopePath={path}&version={assetsProducedInCommit}&includeContent=true&versionType=commit&recursionLevel=full", _logger);

            dynamic content = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
            List<VstsItem> items = JsonConvert.DeserializeObject<List<VstsItem>>(Convert.ToString(content.value));

            foreach (VstsItem item in items)
            {
                if (!item.IsFolder)
                {
                    if (!DependencyFileManager.DependencyFiles.Contains(item.Path))
                    {
                        string fileContent = await GetFileContentAsync(repoName, item.Path);
                        byte[] encodedBytes = Encoding.UTF8.GetBytes(fileContent);
                        GitCommit gitCommit = new GitCommit($"Updating contents of file '{item.Path}'", Convert.ToBase64String(encodedBytes), branch);
                        commits.Add(item.Path, gitCommit);
                    }
                }
            }

            _logger.LogInformation($"Getting the contents of file/files in '{path}' of repo '{repoUri}' at commit '{assetsProducedInCommit}' succeeded!");
        }

        public async Task<string> GetFileContentAsync(string repo, string path)
        {
            string encodedContent;

            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repositories/{repo}/items?path={path}&includeContent=true", _logger);

            dynamic file = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
            encodedContent = file.content;

            return encodedContent;
        }

        public async Task<string> GetLastCommitShaAsync(string repo, string branch)
        {
            HttpResponseMessage response = await this.ExecuteGitCommand(HttpMethod.Get, $"repositories/{repo}/commits?branch={branch}", _logger);

            dynamic content = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());

            List<dynamic> values = JsonConvert.DeserializeObject<List<dynamic>>(Convert.ToString(content.value));

            if (!values.Any())
            {
                throw new Exception($"No commits found in branch '{branch}' of '{repo}'");
            }

            return values[0].commitId;
        }

        public HttpClient CreateHttpClient(string versionOverride = null)
        {
            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri(VstsApiUri)
            };
            client.DefaultRequestHeaders.Add("Accept", $"application/json;api-version={versionOverride ?? DefaultApiVersion}");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", personalAccessToken))));

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
            catch (HttpRequestException exc)
            {
                if (exc.Message.Contains(((int)HttpStatusCode.NotFound).ToString()))
                {
                    return null;
                }

                throw exc;
            }

            dynamic content = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());

            return content.objectId;
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

        private async Task<string> CreateOrUpdatePullRequestAsync(string repoUri, string mergeWithBranch, string sourceBranch, HttpMethod method, int pullRequestId = 0, string title = null, string description = null)
        {
            string linkToPullRquest;
            string requestUri;
            string repoName = SetApiUriAndGetRepoName(repoUri);

            title = !string.IsNullOrEmpty(title) ? $"{PullRequestProperties.TitleTag} {title}" : PullRequestProperties.Title;
            description = description ?? PullRequestProperties.Description;

            VstsPullRequest pullRequest = new VstsPullRequest(title, description, sourceBranch, mergeWithBranch);
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            string body = JsonConvert.SerializeObject(pullRequest, serializerSettings);

            if (method == HttpMethod.Post)
            {
                requestUri = $"repositories/{repoName}/pullrequests";
            }
            else
            {
                requestUri = $"repositories/{repoName}/pullrequests/{pullRequestId}";
            }

            HttpResponseMessage response = await this.ExecuteGitCommand(method, requestUri, _logger, body);

            dynamic content = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
            linkToPullRquest = $"{VstsPrUri}{content.pullRequestId}";

            return linkToPullRquest;
        }
    }
}
