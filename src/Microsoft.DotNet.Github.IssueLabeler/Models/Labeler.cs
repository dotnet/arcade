// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.DotNet.Github.IssueLabeler.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.DotNet.GitHub.IssueLabeler
{
    public class Labeler
    {
        private GitHubClient _client;
        private DiffHelper _diffHelper;
        private DatasetHelper _datasetHelper;
        private Regex _regex;
        private readonly string _repoOwner;
        private readonly string _repoName;
        private readonly double _threshold;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _secretUri;

        public Labeler(string repoOwner, string repoName, string clientId, string clientSecret, string secretUri, double threshold)
        {
            _repoOwner = repoOwner;
            _repoName = repoName;
            _threshold = threshold;
            _clientId = clientId;
            _clientSecret = clientSecret;
            _secretUri = secretUri;
        }

        private async Task GitSetupAsync()
        {
            var kvc = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(
                async (authority, resource, scope) =>
                {
                    var authContext = new AuthenticationContext(authority);
                    var clientCred = new ClientCredential(_clientId, _clientSecret);
                    var result = await authContext.AcquireTokenAsync(resource, clientCred);

                    if (result == null)
                        throw new InvalidOperationException("Failed to obtain the github token");

                    return result.AccessToken;
                }));

            SecretBundle secretBundle = await kvc.GetSecretAsync(_secretUri);

            var productInformation = new ProductHeaderValue("MLGitHubLabeler");
            _client = new GitHubClient(productInformation)
            {
                Credentials = new Credentials(secretBundle.Value)
            };
        }

        public async Task PredictAndApplyLabelAsync(int number, string title, string body, GithubObjectType issueOrPr, ILogger logger)
        {
            if (_client == null)
            {
                await GitSetupAsync();
            }
            if (_regex == null)
            {
                _regex = new Regex(@"@[a-zA-Z0-9_//-]+");
            }
            var userMentions = _regex.Matches(body).Select(x => x.Value).ToArray();

            string label;
            if (issueOrPr == GithubObjectType.Issue)
            {
                IssueModel issue = CreateIssue(number, title, body, userMentions);
                label = Predictor.Predict(issue, logger, _threshold);
            }
            else
            {
                PrModel pr = await CreatePullRequest(number, title, body, userMentions);
                label = Predictor.Predict(pr, logger, _threshold);
            }

            Issue issueGithubVersion = await _client.Issue.Get(_repoOwner, _repoName, number);
            if (label != null && issueGithubVersion.Labels.Count == 0)
            {
                var issueUpdate = new IssueUpdate();
                issueUpdate.AddLabel(label);
                issueUpdate.Milestone = issueGithubVersion.Milestone?.Number; // The number of milestone associated with the issue.

                await _client.Issue.Update(_repoOwner, _repoName, number, issueUpdate);
            }
            else
            {
                logger.LogInformation($"! The Model is not able to assign the label to the {issueOrPr} {number} confidently.");
            }
        }

        private static IssueModel CreateIssue(int number, string title, string body, string[] userMentions)
        {
            return new IssueModel()
            {
                Number = number,
                Title = title,
                Body = body,
                IsPR = 0,
                UserMentions = string.Join(' ', userMentions),
                NumMentions = userMentions.Length
            };
        }

        private async Task<PrModel> CreatePullRequest(int number, string title, string body, string[] userMentions)
        {
            var pr = new PrModel()
            {
                Number = number,
                Title = title,
                Body = body,
                IsPR = 1,
                UserMentions = string.Join(' ', userMentions),
                NumMentions = userMentions.Length,
            };
            IReadOnlyList<PullRequestFile> prFiles = await _client.PullRequest.Files(_repoOwner, _repoName, number);
            if (prFiles.Count != 0)
            {
                string[] filePaths = prFiles.Select(x => x.FileName).ToArray();
                if (_diffHelper == null)
                {
                    _diffHelper = new DiffHelper(filePaths);
                }
                else
                {
                    _diffHelper.ResetTo(filePaths);
                }
                if (_datasetHelper == null)
                {
                    _datasetHelper = new DatasetHelper();
                }
                pr.Files = _datasetHelper.FlattenIntoColumn(filePaths);
                pr.Filenames = _datasetHelper.FlattenIntoColumn(_diffHelper.Filenames);
                pr.FileExtensions = _datasetHelper.FlattenIntoColumn(_diffHelper.Extensions);
                pr.Folders = _datasetHelper.FlattenIntoColumn(_diffHelper.Folders);
                pr.FolderNames = _datasetHelper.FlattenIntoColumn(_diffHelper.FolderNames);
            }
            pr.FileCount = prFiles.Count;
            return pr;
        }
    }
}
