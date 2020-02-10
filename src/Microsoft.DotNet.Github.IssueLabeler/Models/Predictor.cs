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
            return Predict(issue, ref issuePredEngine, logger, threshold);
        }

        public static string Predict(PrModel issue, ILogger logger, double threshold)
        {
            return Predict(issue, ref prPredEngine, logger, threshold);
        }

        public static string Predict<T>(T issueOrPr, ref PredictionEngine<T, GitHubIssuePrediction> predEngine, ILogger logger, double threshold) 
            where T : IssueModel
        {
            if (predEngine == null)
            {
                MLContext mlContext = new MLContext();
                ITransformer mlModel = mlContext.Model.Load(PrModelPath, out DataViewSchema inputSchema);
                predEngine = mlContext.Model.CreatePredictionEngine<T, GitHubIssuePrediction>(mlModel);
            }

            GitHubIssuePrediction prediction = predEngine.Predict(issueOrPr);
            float[] probabilities = prediction.Score;
            float maxProbability = probabilities.Max();
            string typeToPredict = issueOrPr is IssueModel ? "issue" : "PR";
            logger.LogInformation($"# {maxProbability} {prediction.Area} for {typeToPredict} #{issueOrPr.Number} {issueOrPr.Title}");
            return maxProbability > threshold ? prediction.Area : null;
        }
    }
}
