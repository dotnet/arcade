// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.DarcLib
{
    public class DependencyFileContentContainer
    {
        public DependencyFileContent VersionDetailsXml { get; set; }

        public DependencyFileContent VersionProps { get; set; }

        public DependencyFileContent GlobalJson { get; set; }

        public Dictionary<string, GitCommit> GetFilesToCommitMap(string branch, string message = null)
        {
            Dictionary<string, GitCommit> gitHubCommitsMap = new Dictionary<string, GitCommit>
            {
                { VersionDetailsXml.FilePath, VersionDetailsXml.ToCommit(branch, message) },
                { VersionProps.FilePath, VersionProps.ToCommit(branch, message) },
                { GlobalJson.FilePath, GlobalJson.ToCommit(branch, message) }
            };

            return gitHubCommitsMap;
        }
    }
}
