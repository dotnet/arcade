// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Microsoft.ML;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.GitHub.IssueLabeler
{
    internal class Predictor
    {
        private static string ModelPath => @"model\GitHubIssueLabelerModel.zip";
        
        public static async Task<string> PredictAsync(GitHubIssue issue)
        {
            PredictionModel<GitHubIssue, GitHubIssuePrediction> model = await PredictionModel.ReadAsync<GitHubIssue, GitHubIssuePrediction>(ModelPath);
            GitHubIssuePrediction prediction = model.Predict(issue);
      
            float[] probabilities = prediction.Probabilities;
            WebhookIssueController.Logger.LogInformation($"Label for {issue.ID} is predicted with confidence {probabilities.Max().ToString()}");

            return probabilities.Max() > 0.8 ? prediction.Area : null;
        }
    }
}
