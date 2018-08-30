// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Maestro.Client;
using Microsoft.DotNet.Maestro.Client.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Maestro.Tasks
{
    public class PushMetadataToBuildAssetRegistry : MSBuild.Task
    {
        [Required]
        public string ManifestsPath { get; set; }

        [Required]
        public string BuildAssetRegistryToken { get; set; }

        [Required]
        public string MaestroApiEndpoint { get; set; }

        public string AssetLocationType { get; set; } = "NugetFeed";

        private static readonly CancellationTokenSource s_tokenSource = new CancellationTokenSource();
        private static readonly CancellationToken s_cancellationToken = s_tokenSource.Token;

        public override bool Execute()
        {
            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }

        public async Task ExecuteAsync()
        {
            if (s_cancellationToken.IsCancellationRequested)
            {
                Log.LogError("Task PushMetadataToBuildAssetRegistry was cancelled...");
                s_cancellationToken.ThrowIfCancellationRequested();
            }

            await PushMetadataAsync();
        }

        public async Task<bool> PushMetadataAsync()
        {
            try
            {
                Log.LogMessage(MessageImportance.High, "Starting build metadata push to the Build Asset Registry...");

                if (!Directory.Exists(ManifestsPath))
                {
                    Log.LogError($"Required folder '{ManifestsPath}' does not exist.");
                }
                else
                {
                    List<BuildData> buildsManifestMetadata = GetBuildManifestsMetadata(ManifestsPath);

                    BuildData finalBuild = MergeBuildManifests(buildsManifestMetadata);

                    MaestroApi client = (MaestroApi)ApiFactory.GetAuthenticated(MaestroApiEndpoint, BuildAssetRegistryToken);
                    Builds buildAssetRegistryBuilds = new Builds(client);

                    Client.Models.Build recordedBuild = await buildAssetRegistryBuilds.CreateAsync(finalBuild, s_cancellationToken);

                    Log.LogMessage(MessageImportance.High, $"Metadata has been pushed. Build id in the Build Asset Registry is '{recordedBuild.Id}'");
                }
            }
            catch (Exception exc)
            {
                Log.LogErrorFromException(exc, true);
            }

            return !Log.HasLoggedErrors;
        }

        private string GetVersion(string assetId)
        {
            string version = null;

            Match versionMatch = Regex.Match(assetId, @"(?<version>\d+(\.\d+).+?(?=\.))");

            if (versionMatch.Success)
            {
                version = versionMatch.Groups["version"].Value;
            }

            return version;
        }

        private List<BuildData> GetBuildManifestsMetadata(string manifestsFolderPath)
        {
            List<BuildData> buildsManifestMetadata = new List<BuildData>();

            foreach (string manifestPath in Directory.GetFiles(manifestsFolderPath))
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(Manifest));

                using (FileStream stream = new FileStream(manifestPath, FileMode.Open))
                {
                    Manifest manifest = (Manifest)xmlSerializer.Deserialize(stream);

                    List<AssetData> assets = new List<AssetData>();

                    foreach (Package package in manifest.Packages)
                    {
                        AddAsset(assets, package.Id, package.Version, manifest.Location);
                    }

                    buildsManifestMetadata.Add(new BuildData(manifest.Name, manifest.Commit, manifest.BuildId, manifest.Branch, assets, new List<int?>()));
                }
            }

            return buildsManifestMetadata;
        }

        private void AddAsset(List<AssetData> assets, string assetName, string version, string location)
        {
            assets.Add(new AssetData
            {
                Locations = new List<AssetLocationData>
                                    {
                                        new AssetLocationData(location, AssetLocationType)
                                    },
                Name = assetName,
                Version = version,
            });
        }

        private BuildData MergeBuildManifests(List<BuildData> buildsMetadata)
        {
            BuildData mergedBuild = buildsMetadata[0];

            for (int i = 1; i < buildsMetadata.Count; i++)
            {
                BuildData build = buildsMetadata[i];

                if (mergedBuild.Branch != build.Branch ||
                    mergedBuild.BuildNumber != build.BuildNumber ||
                    mergedBuild.Commit != build.Commit ||
                    mergedBuild.Repository != build.Repository)
                {
                    throw new Exception("Can't merge if one or more manifests have different branch, build number, commit or repository values.");
                }

                ((List<AssetData>)mergedBuild.Assets).AddRange(build.Assets);
            }

            mergedBuild.Repository = GetRepoUrl(mergedBuild.Repository, mergedBuild.Commit);

            return mergedBuild;
        }

        private string GetRepoUrl(string repoUrl, string lastCommitHash)
        {
            Uri uri = new Uri(repoUrl);

            if (uri.Host == "github.com")
            {
                return repoUrl;
            }

            using (HttpClient client = new HttpClient())
            {
                string[] segments = repoUrl.Split('/');
                string repoName = segments[segments.Length - 1].Replace("-", "/");

                client.BaseAddress = new Uri("https://api.github.com");
                client.DefaultRequestHeaders.Add("User-Agent", "PushToBarTask");

                HttpResponseMessage response = client.GetAsync($"/repos/{repoName}/commits/{lastCommitHash}").Result;

                if (response.IsSuccessStatusCode)
                {
                    return $"https://github.com/{repoName}";
                }

                return repoUrl;
            }
        }
    }
}
