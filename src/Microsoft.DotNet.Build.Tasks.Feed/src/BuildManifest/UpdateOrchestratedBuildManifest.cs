// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Automation.GitHubApi;
using Microsoft.DotNet.VersionTools.BuildManifest;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks.Feed.BuildManifest
{
    public class UpdateOrchestratedBuildManifest : Task
    {
        private enum UpdateType
        {
            /// <summary>
            /// Find an endpoint with the exact same attributes as the one specified by this item
            /// and merge artifact data. If no matching endpoint exists, create it.
            /// 
            /// %(Xml) must be the Xml representation of an Endpoint manifest element.
            /// </summary>
            AddOrMergeEndpoint
        }

        public const string XmlMetadataName = "Xml";
        private const string JoinSemaphorePathMetadataName = "JoinSemaphorePath";

        /// <summary>
        /// Updates to perform on the manifest. The metadata 'UpdateType' selects a type of update,
        /// and additional metadata is required depending on the type. See UpdateType enum.
        /// </summary>
        public ITaskItem[] ManifestUpdates { get; set; }

        [Required]
        public string VersionsRepoPath { get; set; }

        /// <summary>
        /// Semaphores to update, relative to VersionsRepoPath.
        /// </summary>
        [Required]
        public string[] SemaphoreNames { get; set; }

        [Required]
        public string OrchestratedBuildId { get; set; }

        [Required]
        public string GitHubAuthToken { get; set; }
        public string GitHubUser { get; set; }
        public string GitHubEmail { get; set; }

        public string VersionsRepo { get; set; }
        public string VersionsRepoOwner { get; set; }

        public string VersionsRepoBranch { get; set; }

        public string CommitMessage { get; set; }

        public string OrchestratedIdentitySummary { get; set; }

        /// <summary>
        /// %(Identity): A file to upload to the versions repo.
        /// %(RelativePath): Optional path to upload the file to, relative to VersionsRepoPath.
        ///   If it begins with '/', it is treated as an absolute path within the versions repo.
        ///   '\' is automatically converted to '/'.
        /// </summary>
        public ITaskItem[] SupplementaryFiles { get; set; }

        /// <summary>
        /// "Join semaphore" groups. A join semaphore is created when all semaphores in the group
        /// are complete for a certain build.
        /// 
        /// %(Identity): A semaphore name that is part of a join semaphore group.
        /// %(JoinSemaphorePath): The name of the join semaphore, created when the parallel work joins.
        /// </summary>
        public ITaskItem[] JoinSemaphoreGroups { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrEmpty(CommitMessage))
            {
                string semaphores = string.Join(", ", SemaphoreNames);
                string identity = OrchestratedIdentitySummary ?? VersionsRepoPath;

                CommitMessage = $"Update {identity}: {semaphores}";
            }
            var gitHubAuth = new GitHubAuth(GitHubAuthToken, GitHubUser, GitHubEmail);
            using (var gitHubClient = new GitHubClient(gitHubAuth))
            {
                var client = new BuildManifestClient(gitHubClient);
                PushChangeAsync(client).Wait();
            }
            return !Log.HasLoggedErrors;
        }

        private async System.Threading.Tasks.Task PushChangeAsync(BuildManifestClient client)
        {
            try
            {
                var location = new BuildManifestLocation(
                    new GitHubProject(VersionsRepo, VersionsRepoOwner),
                    $"heads/{VersionsRepoBranch}",
                    VersionsRepoPath);

                IEnumerable<JoinSemaphoreGroup> joinGroups = JoinSemaphoreGroups
                    .Select(item => new
                    {
                        ParallelPartPath = item.ItemSpec,
                        JoinSemaphorePath = item.GetMetadata(JoinSemaphorePathMetadataName)
                    })
                    .GroupBy(j => j.JoinSemaphorePath, j => j.ParallelPartPath)
                    .Select(g => new JoinSemaphoreGroup
                    {
                        JoinSemaphorePath = g.Key,
                        ParallelSemaphorePaths = g
                    });

                SupplementaryUploadRequest[] supplementaryUploads =
                    PushOrchestratedBuildManifest.CreateUploadRequests(SupplementaryFiles);

                var change = new BuildManifestChange(
                    location,
                    CommitMessage,
                    OrchestratedBuildId,
                    SemaphoreNames,
                    manifest =>
                    {
                        foreach (var update in ManifestUpdates ?? Enumerable.Empty<ITaskItem>())
                        {
                            ApplyUpdate(manifest, update);
                        }
                    })
                {
                    SupplementaryUploads = supplementaryUploads,
                    JoinSemaphoreGroups = joinGroups,
                };

                await client.PushChangeAsync(change);
            }
            catch (ManifestChangeOutOfDateException e)
            {
                Log.LogWarningFromException(e);
            }
        }

        private static void ApplyUpdate(OrchestratedBuildModel manifest, ITaskItem update)
        {
            string type = update.GetMetadata(nameof(UpdateType));
            UpdateType updateType;

            if (!Enum.TryParse(type, true, out updateType))
            {
                throw new ArgumentException(
                    $"UpdateType '{type}' on update '{update.ItemSpec}' is not valid.");
            }

            switch (updateType)
            {
                case UpdateType.AddOrMergeEndpoint:
                    var xml = XElement.Parse(update.GetMetadata(XmlMetadataName));
                    EndpointModel endpoint = EndpointModel.Parse(xml);

                    EndpointModel existingEndpoint = manifest.Endpoints
                        .FirstOrDefault(e => SameAttributes(endpoint.Attributes, e.Attributes));

                    if (existingEndpoint == null)
                    {
                        manifest.Endpoints.Add(endpoint);
                    }
                    else
                    {
                        existingEndpoint.Artifacts.Add(endpoint.Artifacts);
                    }
                    break;
            }
        }

        private static bool SameAttributes(
            IDictionary<string, string> a,
            IDictionary<string, string> b)
        {
            return a.Count == b.Count && !a.Except(b).Any();
        }
    }
}
