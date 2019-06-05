// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Microsoft.ML;
using System.Linq;

namespace Microsoft.DotNet.GitHub.IssueLabeler
{
    internal static class Predictor
    {
        private static string ModelPath => @"model\GitHubIssueLabelerModel.zip";
        private static PredictionEngine<GitHubIssue, GitHubIssuePrediction> predEngine;

        public static string Predict(GitHubIssue issue, ILogger logger, double threshold)
        {
            if (predEngine == null)
            {
                MLContext mlContext = new MLContext();
                ITransformer mlModel = mlContext.Model.Load(ModelPath, out DataViewSchema inputSchema);
                predEngine = mlContext.Model.CreatePredictionEngine<GitHubIssue, GitHubIssuePrediction>(mlModel);
            }

            GitHubIssuePrediction prediction = predEngine.Predict(issue);
            float[] probabilities = prediction.Score;
            float maxProbability = probabilities.Max();
            logger.LogInformation($"# {maxProbability.ToString()} {prediction.Area} for #{issue.Number} {issue.Title}");
            return maxProbability > threshold ? prediction.Area : null;
        }
    }
}
