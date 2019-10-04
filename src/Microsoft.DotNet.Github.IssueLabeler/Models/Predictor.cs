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
        private static string PrModelPath => @"model\GitHubPrLabelerModel.zip";
        private static string IssueModelPath => @"model\GitHubIssueLabelerModel.zip";
        private static PredictionEngine<IssueModel, GitHubIssuePrediction> issuePredEngine;
        private static PredictionEngine<PrModel, GitHubIssuePrediction> prPredEngine;

        public static string Predict(IssueModel issue, ILogger logger, double threshold)
        {
            if (issuePredEngine == null)
            {
                MLContext mlContext = new MLContext();
                ITransformer mlModel = mlContext.Model.Load(IssueModelPath, out DataViewSchema inputSchema);
                issuePredEngine = mlContext.Model.CreatePredictionEngine<IssueModel, GitHubIssuePrediction>(mlModel);
            }

            GitHubIssuePrediction prediction = issuePredEngine.Predict(issue);
            float[] probabilities = prediction.Score;
            float maxProbability = probabilities.Max();
            logger.LogInformation($"# {maxProbability} {prediction.Area} for issue #{issue.Number} {issue.Title}");
            return maxProbability > threshold ? prediction.Area : null;
        }

        public static string Predict(PrModel issue, ILogger logger, double threshold)
        {
            if (prPredEngine == null)
            {
                MLContext mlContext = new MLContext();
                ITransformer mlModel = mlContext.Model.Load(PrModelPath, out DataViewSchema inputSchema);
                prPredEngine = mlContext.Model.CreatePredictionEngine<PrModel, GitHubIssuePrediction>(mlModel);
            }

            GitHubIssuePrediction prediction = prPredEngine.Predict(issue);
            float[] probabilities = prediction.Score;
            float maxProbability = probabilities.Max();
            logger.LogInformation($"# {maxProbability} {prediction.Area} for PR #{issue.Number} {issue.Title}");
            return maxProbability > threshold ? prediction.Area : null;
        }
    }
}
