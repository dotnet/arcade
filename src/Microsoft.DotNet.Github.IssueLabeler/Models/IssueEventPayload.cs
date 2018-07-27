// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Serialization;

namespace Microsoft.DotNet.GitHub.IssueLabeler
{
    public class IssueEventPayload
    {
        [DataMember(Name = "action")]
        public string Action { set; get; }

        [DataMember(Name = "issue")]
        public GitHubIssue Issue { set; get; }
    }
}
