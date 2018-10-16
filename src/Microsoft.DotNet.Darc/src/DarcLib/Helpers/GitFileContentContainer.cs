// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.DarcLib
{
    public class GitFileContentContainer
    {
        public GitFile VersionDetailsXml { get; set; }

        public GitFile VersionProps { get; set; }

        public GitFile GlobalJson { get; set; }

        public List<GitFile> GetFilesToCommit()
        {
            var gitHubCommitsMap = new List<GitFile>
            {
                VersionDetailsXml,
                VersionProps,
                GlobalJson
            };

            return gitHubCommitsMap;
        }
    }
}
