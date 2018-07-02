using System.Collections.Generic;

namespace Microsoft.DotNet.Darc
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
