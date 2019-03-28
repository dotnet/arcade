// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Octokit;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Git.IssueManager.Clients
{
    static class GitHubClient
    {
        public static async Task<string> GetCommitAuthorAsync(
            string repositoryUrl,
            string commit,
            string personalAccessToken)
        {
            (string owner, string repoName) = ParseRepoUri(repositoryUrl);

            Octokit.GitHubClient client = new Octokit.GitHubClient(new ProductHeaderValue("assets-publisher"));
            Credentials tokenAuth = new Credentials(personalAccessToken);
            client.Credentials = tokenAuth;

            GitHubCommit commitInfo = await client.Repository.Commit.Get(owner, repoName, commit);

            while (commitInfo.Author.Type == "Bot")
            {
                if (!commitInfo.Parents.Any()) break;
                commit = commitInfo.Parents.First().Sha;
                commitInfo = await client.Repository.Commit.Get(owner, repoName, commit);
            }

            return $"@{commitInfo.Author.Login}";
        }

        public static async Task<int> CreateNewIssueAsync(
            string repositoryUrl,
            string issueTitle,
            string issueDescription,
            string personalAccessToken)
        {
            (string owner, string repoName) = ParseRepoUri(repositoryUrl);

            Octokit.GitHubClient client = new Octokit.GitHubClient(new ProductHeaderValue("assets-publisher"));
            Credentials tokenAuth = new Credentials(personalAccessToken);
            client.Credentials = tokenAuth;

            NewIssue issueToBeCreated = new NewIssue(issueTitle)
            {
                Body = issueDescription
            };

            Issue createdIssue = await client.Issue.Create(owner, repoName, issueToBeCreated);

            return createdIssue.Number;
        }

        /// <summary>
        /// Extracts the owner and repository name from <paramref name="repositoryUri"/>. 
        /// </summary>
        /// <param name="repositoryUri">The repository URI.</param>
        /// <returns>The owner and the repository name</returns>
        private static (string owner, string repositoryName) ParseRepoUri(string repositoryUri)
        {
            Regex repositoryUriPattern = new Regex(@"^/(?<owner>[^/]+)/(?<repo>[^/]+)/?$");
            Uri uri = new Uri(repositoryUri);

            Match match = repositoryUriPattern.Match(uri.AbsolutePath);

            if (!match.Success)
            {
                return default;
            }

            return (match.Groups["owner"].Value, match.Groups["repo"].Value);
        }
    }
}
