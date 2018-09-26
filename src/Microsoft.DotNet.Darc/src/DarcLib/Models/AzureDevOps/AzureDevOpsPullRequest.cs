// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DarcLib
{
    public class AzureDevOpsPullRequest
    {
        public AzureDevOpsPullRequest(string title, string description, string sourceBranch, string targetBranch)
        {
            Title = title;
            Description = description;
            SourceRefName = $"refs/heads/{sourceBranch}";
            TargetRefName = $"refs/heads/{targetBranch}";
        }

        public string Title { get; set; }

        public string Description { get; set; }

        public string SourceRefName { get; set; }

        public string TargetRefName { get; set; }
    }
}
