// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DarcLib
{
    public class GitHubRef
    {
        public GitHubRef(string githubRef, string sha)
        {
            Ref = githubRef;
            Sha = sha;
        }

        public string Ref { get; set; }

        public string Sha { get; set; }

        public bool Force { get; set; }
    }
}
