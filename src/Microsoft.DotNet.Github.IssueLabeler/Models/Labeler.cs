// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Octokit;
using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.GitHub.IssueLabeler
{
    public class Labeler
    {
        private GitHubClient _client;
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

            var corefxIssue = new GitHubIssue
            {
                Number = number,
                Title = title,
                Body = body,
                IssueOrPr = issueOrPr
            };

            string label = Predictor.Predict(corefxIssue, logger, _threshold);
            Issue issueGithubVersion = await _client.Issue.Get(_repoOwner, _repoName, number);
            if (label.Equals("area-System.Net.Http.SocketsHttpHandler", StringComparison.OrdinalIgnoreCase))
            {
                label = "area-System.Net.Http";
            }

            if (label != null && issueGithubVersion.Labels.Count == 0)
            {
                var issueUpdate = new IssueUpdate();
                issueUpdate.AddLabel(label);
                issueUpdate.Milestone = issueGithubVersion.Milestone?.Number; // The number of milestone associated with the issue.

                await _client.Issue.Update(_repoOwner, _repoName, number, issueUpdate);
            }
            else
            {
                logger.LogInformation($"! The Model is not able to assign the label to the {issueOrPr} {corefxIssue.Number} confidently.");
            }
        }
    }
}
