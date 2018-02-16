// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks.Feed.BuildManifest
{
    public class WriteOrchestratedBuildManifestToFile : Task
    {
        [Required]
        public string File { get; set; }

        [Required]
        public string BlobFeedUrl { get; set; }

        [Required]
        public ITaskItem[] BuildManifestFiles { get; set; }

        [Required]
        public string ManifestName { get; set; }

        [Required]
        public string ManifestBuildId { get; set; }

        public string ManifestBranch { get; set; }
        public string ManifestCommit { get; set; }

        public override bool Execute()
        {
            // Leave out attributes if they would just have empty string values.
            if (ManifestBranch == string.Empty)
            {
                ManifestBranch = null;
            }
            if (ManifestCommit == string.Empty)
            {
                ManifestCommit = null;
            }

            var identity = new BuildIdentity
            {
                Name = ManifestName,
                BuildId = ManifestBuildId,
                Branch = ManifestBranch,
                Commit = ManifestCommit
            };

            var orchestratedBuild = new OrchestratedBuildModel(identity)
            {
                Endpoints = new List<EndpointModel>
                {
                    EndpointModel.CreateOrchestratedBlobFeed(BlobFeedUrl)
                }
            };

            foreach (ITaskItem buildManifestFile in BuildManifestFiles)
            {
                string contents = System.IO.File.ReadAllText(buildManifestFile.ItemSpec);

                BuildModel build = BuildModel.Parse(XElement.Parse(contents));

                foreach (PackageArtifactModel package in build.Artifacts.Packages)
                {
                    package.OriginBuildName = build.Identity.Name;
                }

                orchestratedBuild.AddParticipantBuild(build);
            }

            System.IO.File.WriteAllText(File, orchestratedBuild.ToXml().ToString());

            return !Log.HasLoggedErrors;
        }
    }
}
