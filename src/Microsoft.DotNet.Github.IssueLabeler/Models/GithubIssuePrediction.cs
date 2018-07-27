// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.ML.Runtime.Api;

namespace Microsoft.DotNet.GitHub.IssueLabeler
{
    public class GitHubIssuePrediction
    {
        [ColumnName("PredictedLabel")]
        public string Area;

        [ColumnName("Score")]
        public float[] Probabilities;
    }
}
