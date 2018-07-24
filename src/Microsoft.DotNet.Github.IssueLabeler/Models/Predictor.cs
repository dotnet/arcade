// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.ML;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Github.IssueLabeler
{
    internal class Predictor
    {
        private static string ModelPath => Path.Combine(Directory.GetCurrentDirectory(), "GitHubLabelerModel.zip");
        private static PredictionModel<GitHubIssue, GitHubIssuePrediction> _model;

        public static async Task<string> PredictAsync(GitHubIssue issue)
        {
            if (_model == null)
            {
                _model = await PredictionModel.ReadAsync<GitHubIssue, GitHubIssuePrediction>(ModelPath);
            }

            GitHubIssuePrediction prediction = _model.Predict(issue);
            float[] probs = prediction.Probs;

            return probs.Max() > 0.1 ? prediction.Area : null;
        }
    }
}
