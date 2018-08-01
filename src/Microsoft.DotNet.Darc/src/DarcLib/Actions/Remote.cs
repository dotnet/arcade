// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    public class RemoteActions : IRemote
    {
        private readonly BuildAssetRegistryClient _barClient;
        private readonly DependencyFileManager _fileManager;
        private readonly IGitRepo _gitClient;
        private readonly ILogger _logger;

        public RemoteActions(DarcSettings settings, ILogger logger)
        {
            ValidateSettings(settings);

            _logger = logger;

            if (settings.GitType == GitRepoType.GitHub)
            {
                _gitClient = new GitHubClient(settings.PersonalAccessToken, _logger);
            }
            else
            {
                _gitClient = new VstsClient(settings.PersonalAccessToken, _logger);
            }

            _fileManager = new DependencyFileManager(_gitClient, _logger);
            _barClient = new BuildAssetRegistryClient(settings.BuildAssetRegistryPassword, settings.BuildAssetRegistryBaseUri, _logger);
        }

        public async Task<string> CreateChannelAsync(string name, string classification)
        {
            return await _barClient.CreateChannelAsync(name, classification);
        }

        public async Task<string> GetSubscriptionsAsync(string sourceRepo = null, string targetRepo = null, int? channelId = null)
        {
            return await _barClient.GetSubscriptionsAsync(sourceRepo, targetRepo, channelId);
        }

        public async Task<string> GetSubscriptionAsync(int subscriptionId)
        {
            return await _barClient.GetSubscriptionAsync(subscriptionId);
        }

        public async Task<string> CreateSubscriptionAsync(string channelName, string sourceRepo, string targetRepo, string targetBranch, string updateFrequency, string mergePolicy)
        {
            return await _barClient.CreateSubscriptionAsync(channelName, sourceRepo, targetRepo, targetBranch, updateFrequency, mergePolicy);
        }

        public async Task<IEnumerable<DependencyDetail>> GetRequiredUpdatesAsync(string repoUri, string branch)
        {
            _logger.LogInformation($"Getting dependencies which need to be updated in repo '{repoUri}' and branch '{branch}'...");

            List<DependencyDetail> toUpdate = new List<DependencyDetail>();
            IEnumerable<DependencyDetail> dependencyDetails = await _fileManager.ParseVersionDetailsXmlAsync(repoUri, branch);

            foreach (DependencyDetail dependency in dependencyDetails)
            {
                string latestBuildContent = await _barClient.GetLastestBuildAsync(repoUri, branch, dependency.Name);

                if (!string.IsNullOrEmpty(latestBuildContent))
                {
                    BuildData buildData = JsonConvert.DeserializeObject<BuildData>(latestBuildContent);

                    if (buildData.Assets.Count == 0)
                    {
                        throw new Exception("A build eas returned but contained no assets.");
                    }

                    AssetData asset = buildData.Assets.Where(a => a.Name == dependency.Name).FirstOrDefault();

                    if (asset == null)
                    {
                        throw new Exception($"No asset found matching name '{dependency.Name}' was found in the returned build object.");
                    }

                    if (string.Compare(asset.Version, dependency.Version) == 1)
                    {
                        dependency.Version = asset.Version;
                        dependency.Commit = buildData.Commit;
                        toUpdate.Add(dependency);
                    }
                }
                else
                {
                    _logger.LogWarning($"No asset with name '{dependency.Name}' found in store but it is defined in repo '{repoUri}' and branch '{branch}'.");
                }
            }

            _logger.LogInformation($"Getting dependencies which need to be updated in repo '{repoUri}' and branch '{branch}' succeeded!");

            return toUpdate;
        }

        public async Task<string> CreatePullRequestAsync(IEnumerable<DependencyDetail> itemsToUpdate, string repoUri, string branch, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null)
        {
            _logger.LogInformation($"Create pull request to update dependencies in repo '{repoUri}' and branch '{branch}'...");

            string linkToPr = null;

            await _gitClient.CreateDarcBranchAsync(repoUri, branch);

            pullRequestBaseBranch = pullRequestBaseBranch ?? $"darc-{branch}";

            // Check for exsting PRs in the darc created branch. If there is one under the same user we fail fast before commiting files that won't be included in a PR. 
            string existingPr = await _gitClient.CheckForOpenPullRequestsAsync(repoUri, pullRequestBaseBranch);

            if (string.IsNullOrEmpty(existingPr))
            {
                await CommitFilesForPullRequest(itemsToUpdate, repoUri, branch, pullRequestBaseBranch, pullRequestTitle, pullRequestDescription);

                linkToPr = await _gitClient.CreatePullRequestAsync(repoUri, branch, pullRequestBaseBranch, pullRequestTitle, pullRequestDescription);

                _logger.LogInformation($"Updating dependencies in repo '{repoUri}' and branch '{branch}' succeeded! PR link is: {linkToPr}");

                return linkToPr;
            }

            _logger.LogError($"PR with link '{existingPr}' is already opened in repo '{repoUri}' and branch '{pullRequestBaseBranch}' please update it instead of trying to create a new one");

            return linkToPr;
        }

        public async Task<string> UpdatePullRequestAsync(IEnumerable<DependencyDetail> itemsToUpdate, string repoUri, string branch, int pullRequestId, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null)
        {
            _logger.LogInformation($"Updating pull request '{pullRequestId}' in repo '{repoUri}' and branch '{branch}'...");
            string linkToPr = null;

            pullRequestBaseBranch = pullRequestBaseBranch ?? $"darc-{branch}";

            await CommitFilesForPullRequest(itemsToUpdate, repoUri, branch, pullRequestBaseBranch, pullRequestTitle, pullRequestDescription);

            linkToPr = await _gitClient.UpdatePullRequestAsync(repoUri, branch, pullRequestBaseBranch, pullRequestId, pullRequestTitle, pullRequestDescription);

            _logger.LogInformation($"Updating dependencies in repo '{repoUri}' and branch '{branch}' succeeded! PR link is: {linkToPr}");

            return linkToPr;
        }

        private void ValidateSettings(DarcSettings settings)
        {
            if (string.IsNullOrEmpty(settings.BuildAssetRegistryPassword))
            {
                throw new ArgumentException("A B.A.R password is mandatory for remote actions and is missing...");
            }

            if (string.IsNullOrEmpty(settings.BuildAssetRegistryPassword))
            {
                throw new ArgumentException("The personal access token is missing...");
            }
        }

        private async Task<Dictionary<string, GitCommit>> GetScriptCommitsAsync(string repoUri, string branch, string assetName = "Microsoft.DotNet.Arcade.Sdk")
        {
            _logger.LogInformation($"Generating commits for script files");

            string latestBuildContent = await _barClient.GetLastestBuildAsync(repoUri, branch, assetName);

            dynamic latestBuild = JsonConvert.DeserializeObject<dynamic>(latestBuildContent);

            Dictionary<string, GitCommit> commits = await _gitClient.GetCommitsForPathAsync(repoUri, latestBuild.commit, branch);

            _logger.LogInformation($"Generating commits for script files succeeded!");

            return commits;
        }

        private async Task CommitFilesForPullRequest(IEnumerable<DependencyDetail> itemsToUpdate, string repoUri, string branch, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null)
        {
            DependencyFileContentContainer fileContainer = await _fileManager.UpdateDependencyFiles(itemsToUpdate, repoUri, branch);
            Dictionary<string, GitCommit> dependencyFilesToCommit = fileContainer.GetFilesToCommitMap(pullRequestBaseBranch);

            await _gitClient.PushFilesAsync(dependencyFilesToCommit, repoUri, pullRequestBaseBranch);

            // If there is an arcade asset that we need to update we try to update the script files as well
            DependencyDetail arcadeItem = itemsToUpdate.Where(i => i.Name.ToLower().Contains("arcade")).FirstOrDefault();

            if (arcadeItem != null)
            {
                await _gitClient.PushFilesAsync(await GetScriptCommitsAsync(repoUri, branch, assetName: arcadeItem.Name), repoUri, pullRequestBaseBranch);
            }
        }
    }
}
