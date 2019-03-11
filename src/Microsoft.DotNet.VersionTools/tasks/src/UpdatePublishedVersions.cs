// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.VersionTools.Automation;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.VersionTools
{
    public class UpdatePublishedVersions : Task
    {
        [Required]
        public ITaskItem[] ShippedNuGetPackage { get; set; }

        [Required]
        public string VersionsRepoPath { get; set; }

        [Required]
        public string GitHubAuthToken { get; set; }
        public string GitHubUser { get; set; }
        public string GitHubEmail { get; set; }

        public string VersionsRepo { get; set; }
        public string VersionsRepoOwner { get; set; }

        public override bool Execute()
        {
            Trace.Listeners.MsBuildListenedInvoke(Log, () =>
            {
                var gitHubAuth = new GitHubAuth(GitHubAuthToken, GitHubUser, GitHubEmail);

                var updater = new GitHubVersionsRepoUpdater(gitHubAuth, VersionsRepoOwner, VersionsRepo);

                updater.UpdateBuildInfoAsync(
                    ShippedNuGetPackage.Select(item => item.ItemSpec),
                    VersionsRepoPath)
                    .Wait();
            });
            return true;
        }
    }
}
