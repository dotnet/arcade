// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.ML.Runtime.Api;

namespace Microsoft.DotNet.Github.IssueLabeler
{
    public class GitHubIssue
    {
        [Column(ordinal: "0")]
        public string ID;

        [Column(ordinal: "1")]
        public string Area;

        [Column(ordinal: "2")]
        public string Title;

        [Column(ordinal: "3")]
        public string Description;
    }
}
