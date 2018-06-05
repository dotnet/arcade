using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc
{
    public class GitHubClient
    {
        private readonly string personalAccessToken;
        private const string GitHubApiUri = "https://api.github.com";
        private const string DarcBranchName = "darc";
        private const string VersionPullRequestTitle = "Darc-Update global.json, version.props and version.details.xml";
        private const string VersionPullRequestDescription = "Darc is trying to update these files to the latest versions found in the Product Dependency Store";

        public GitHubClient(string accessToken)
        {
            personalAccessToken = accessToken;
        }

        public async Task<string> GetFileContentsAsync(string filePath, string repoUri, string branch)
        {
            Console.WriteLine($"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}'...");

            using (HttpClient client = new HttpClient())
            {
                string ownerAndRepo = GetOwnerAndRepo(repoUri);
                client.BaseAddress = new Uri(GitHubApiUri);
                client.DefaultRequestHeaders.Add("Authorization", $"Token {personalAccessToken}");
                client.DefaultRequestHeaders.Add("User-Agent", "DarcLib");

                HttpResponseMessage response = await client.GetAsync($"repos/{ownerAndRepo}contents/{filePath}?ref={branch}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}' failed with code '{response.StatusCode}'");
                    response.EnsureSuccessStatusCode();
                }

                Console.WriteLine($"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}' succeeded!");

                return GetDecodedContent(await response.Content.ReadAsStringAsync());
            }
        }

        public async Task<bool> CreateDarcBranchAsync(string repoUri)
        {
            Console.WriteLine($"Verifying if '{DarcBranchName}' branch exist in repo '{repoUri}'. If not, we'll create it...");

            using (HttpClient client = new HttpClient())
            {
                string ownerAndRepo = GetOwnerAndRepo(repoUri);
                client.BaseAddress = new Uri(GitHubApiUri);
                client.DefaultRequestHeaders.Add("Authorization", $"Token {personalAccessToken}");
                client.DefaultRequestHeaders.Add("User-Agent", "DarcLib");

                HttpResponseMessage response = await client.GetAsync($"repos/{ownerAndRepo}branches/{DarcBranchName}");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        Console.WriteLine($"'{DarcBranchName}' branch doesn't exist. Creating it...");
                        string latestSha = await GetLastCommitShaAsync(ownerAndRepo);
                        GitHubRef githubRef = new GitHubRef($"refs/heads/{DarcBranchName}", latestSha);

                        JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                        {
                            ContractResolver = new CamelCasePropertyNamesContractResolver()
                        };
                        string body = JsonConvert.SerializeObject(githubRef, serializerSettings);

                        response = await client.PostAsync($"repos/{ownerAndRepo}git/refs", new StringContent(body));

                        if (!response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"Creating branch '{DarcBranchName}' in repo '{repoUri}' from branch 'master' failed with status code '{response.StatusCode}'");
                            response.EnsureSuccessStatusCode();
                        }

                        Console.WriteLine($"Branch '{DarcBranchName}' created in repo '{repoUri}'!");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"Checking if '{DarcBranchName}' branch existed in repo '{repoUri}' failed with code '{response.StatusCode}'");
                        response.EnsureSuccessStatusCode();
                    }
                }

                return true;
            }
        }

        public async Task<bool> PushDependencyFiles(Dictionary<string, GitHubCommit> filesToCommit, string repoUri, string branch)
        {
            using (HttpClient client = new HttpClient())
            {
                string ownerAndRepo = GetOwnerAndRepo(repoUri);
                client.BaseAddress = new Uri(GitHubApiUri);
                client.DefaultRequestHeaders.Add("Authorization", $"Token {personalAccessToken}");
                client.DefaultRequestHeaders.Add("User-Agent", "DarcLib");

                foreach (string filePath in filesToCommit.Keys)
                {
                    GitHubCommit commit = filesToCommit[filePath];
                    string blobSha = await CheckIfFileExistsAsync(repoUri, filePath);

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
                    HttpResponseMessage response = await client.PutAsync($"repos/{ownerAndRepo}contents/{filePath}", new StringContent(body));

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"There was an error while trying to update {filePath}");
                        response.EnsureSuccessStatusCode();
                    }
                }
            }

            return true;
        }

        public async Task<string> CreatePullRequestAsync(string repoUri, string mergeWithBranch, string sourceBranch, string title = null, string description = null)
        {
            string linkToPullRquest;

            using (HttpClient client = new HttpClient())
            {
                string ownerAndRepo = GetOwnerAndRepo(repoUri);
                client.BaseAddress = new Uri(GitHubApiUri);
                client.DefaultRequestHeaders.Add("Authorization", $"Token {personalAccessToken}");
                client.DefaultRequestHeaders.Add("User-Agent", "DarcLib");

                title = title ?? VersionPullRequestTitle;
                description = description ?? VersionPullRequestDescription;

                GitHubPullRequest pullRequest = new GitHubPullRequest(title, description, sourceBranch, mergeWithBranch);
                JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };
                string body = JsonConvert.SerializeObject(pullRequest, serializerSettings);
                HttpResponseMessage response = await client.PostAsync($"repos/{ownerAndRepo}pulls", new StringContent(body));

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"There was an error while trying to create a pull request in '{repoUri}' between branch '{mergeWithBranch}' and '{sourceBranch}'");
                    response.EnsureSuccessStatusCode();
                }

                dynamic content = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
                linkToPullRquest = content.html_url;
            }

            return linkToPullRquest;
        }

        private async Task<string> CheckIfFileExistsAsync(string repoUri, string filePath)
        {
            string sha = null;

            using (HttpClient client = new HttpClient())
            {
                string ownerAndRepo = GetOwnerAndRepo(repoUri);
                client.BaseAddress = new Uri(GitHubApiUri);
                client.DefaultRequestHeaders.Add("Authorization", $"Token {personalAccessToken}");
                client.DefaultRequestHeaders.Add("User-Agent", "DarcLib");

                HttpResponseMessage response = await client.GetAsync($"repos/{ownerAndRepo}contents/{filePath}?ref=darc");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        return sha;
                    }

                    response.EnsureSuccessStatusCode();
                }

                dynamic content = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
                sha = content.sha;
            }

            return sha;
        }

        private string GetOwnerAndRepo(string repoUri)
        {
            return repoUri.Replace("https://github.com/", string.Empty);
        }

        private string GetDecodedContent(string encodedContent)
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            FileContent fileContent = JsonConvert.DeserializeObject<FileContent>(encodedContent, serializerSettings);

            byte[] content = Convert.FromBase64String(fileContent.Content);
            return Encoding.UTF8.GetString(content);
        }

        private async Task<string> GetLastCommitShaAsync(string ownerAndRepo)
        {
            string sha;

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(GitHubApiUri);
                client.DefaultRequestHeaders.Add("Authorization", $"Token {personalAccessToken}");
                client.DefaultRequestHeaders.Add("User-Agent", "DarcLib");

                HttpResponseMessage response = await client.GetAsync($"repos/{ownerAndRepo}commits/master");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Getting the last commit from '{ownerAndRepo}master' failed with code '{response.StatusCode}'");
                    response.EnsureSuccessStatusCode();
                }

                dynamic content = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());
                sha = content.sha;
            }

            return sha;
        }
    }
}
