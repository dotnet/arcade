// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    /// <summary>
    /// This task generates an XML build manifest for a list of packages and files.
    /// </summary>
    public class GenerateBuildManifest : MSBuild.Task
    {
        /// <summary>
        /// An list of files produced by the build.
        /// <para>
        /// Supported metadata values:
        /// 
        ///   * RelativeBlobPath = the expect path of a blob once it has been pushed to blob storage
        ///   * ManifestArtifactData = an arbitrary key=value list of properties
        /// </para>
        /// </summary>
        [Required]
        public ITaskItem[] Artifacts { get; set; }

        /// <summary>
        /// The location where the build XML file will be generated
        /// </summary>
        [Required]
        public string OutputPath { get; set; }

        /// <summary>
        /// List of files that need to be signed
        /// </summary>
        public ITaskItem[] ItemsToSign { get; set; }

        /// <summary>
        /// List of files with strong name sign info and said info
        /// </summary>
        public ITaskItem[] StrongNameSignInfo { get; set; }
        
        /// <summary>
        /// List of which certificates to use when signing individual files
        /// </summary>
        public ITaskItem[] FileSignInfo { get; set; }

        /// <summary>
        /// List of which certificates to use when signing files with particular extensions
        /// </summary>
        public ITaskItem[] FileExtensionSignInfo { get; set; }

        /// <summary>
        /// The CI build ID.
        /// </summary>
        public string BuildId { get; set; } = "no build id provided";

        /// <summary>
        /// Named properties, and arbitrary list of metadata about the current build.
        /// </summary>
        public string[] BuildData { get; set; }

        /// <summary>
        /// The URI of the repository used to produce the artifacts.
        /// </summary>
        public string RepoUri { get; set; }

        /// <summary>
        /// The branch name of the source code used to produce the artifacts.
        /// </summary>
        public string RepoBranch { get; set; }

        /// <summary>
        /// The commit hash of the source code used to produce the artifacts.
        /// </summary>
        public string RepoCommit { get; set; }

        /// <summary>
        /// Is this manifest for a stable build?
        /// </summary>
        public bool IsStableBuild { get; set; }
        
        /// <summary>
        /// The version of the publishing infrastructure that should be tagged in the manifest.
        /// </summary>
        public string PublishingVersion { get; set; }

        public override bool Execute()
        {
            try
            {
                PublishingInfraVersion targetPublishingVersion = PublishingInfraVersion.Legacy;

                if (!string.IsNullOrEmpty(PublishingVersion)) 
                {
                    if (Enum.TryParse(PublishingVersion, ignoreCase: true, out targetPublishingVersion))
                    {
                        Log.LogError($"Could not parse '{PublishingVersion}' as a valid publishing infrastructure version.");
                        return false;
                    }
                }

                var buildModel = BuildManifestUtil.CreateModelFromItems(
                    Artifacts,
                    ItemsToSign,
                    StrongNameSignInfo,
                    FileSignInfo,
                    FileExtensionSignInfo,
                    BuildId,
                    BuildData,
                    RepoUri,
                    RepoBranch,
                    RepoCommit,
                    IsStableBuild,
                    targetPublishingVersion,
                    Log);

                buildModel.WriteAsXml(OutputPath, Log);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }

    }
}
