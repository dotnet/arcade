// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Octokit;
using System.Threading.Tasks;

namespace Microsoft.DotNet.GitHub.IssueLabeler
{
    public class Labeler
    {
        private readonly GitHubClient _client;
        private readonly string _repoOwner;
        private readonly string _repoName;

        public Labeler(string repoOwner, string repoName, string accessToken)
        {
            _repoOwner = repoOwner;
            _repoName = repoName;
            var productInformation = new ProductHeaderValue("MLGitHubLabeler");
            _client = new GitHubClient(productInformation)
            {
                Credentials = new Credentials(accessToken)
            };
        }

        public async Task PredictAndApplyLabelAsync(int number, string title, string body, ILogger logger)
        {
            var corefxIssue = new GitHubIssue
            {
                ID = number.ToString(),
                Title = title,
                Description = body
            };

            string label = await Predictor.PredictAsync(corefxIssue, logger);
            if (label != null)
            {
                var issueUpdate = new IssueUpdate();
                issueUpdate.AddLabel(label);

                await _client.Issue.Update(_repoOwner, _repoName, number, issueUpdate);
            }
            else
            {
                logger.LogInformation($"! The Model is not able to assign the label to the Issue {corefxIssue.ID} confidently.");
            }
        }
    }
}
