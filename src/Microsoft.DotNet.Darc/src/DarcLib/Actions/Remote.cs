// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Maestro.Client;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    public class Remote : IRemote
    {
        private readonly IMaestroApi _barClient;
        private readonly GitFileManager _fileManager;
        private readonly IGitRepo _gitClient;
        private readonly ILogger _logger;

        public Remote(DarcSettings settings, ILogger logger)
        {
            ValidateSettings(settings);

            _logger = logger;

            if (settings.GitType == GitRepoType.GitHub)
            {
                _gitClient = new GitHubClient(settings.PersonalAccessToken, _logger);
            }
            else if (settings.GitType == GitRepoType.AzureDevOps)
            {
                _gitClient = new AzureDevOpsClient(settings.PersonalAccessToken, _logger);
            }

            // Only initialize the file manager if we have a git client, which excludes "None"
            if (_gitClient != null)
            {
                _fileManager = new GitFileManager(_gitClient, _logger);
            }

            // Initialize the bar client if there is a password
            if (!string.IsNullOrEmpty(settings.BuildAssetRegistryPassword))
            {
                if (!string.IsNullOrEmpty(settings.BuildAssetRegistryBaseUri))
                {
                    _barClient = ApiFactory.GetAuthenticated(settings.BuildAssetRegistryBaseUri, settings.BuildAssetRegistryPassword);
                }
                else
                {
                    _barClient = ApiFactory.GetAuthenticated(settings.BuildAssetRegistryPassword);
                }
            }
        }

        /// <summary>
        /// Retrieve the list of channels from the build asset registry.
        /// </summary>
        /// <param name="classification">Optional classification to get</param>
        /// <returns></returns>
        public async Task<IEnumerable<Channel>> GetChannelsAsync(string classification = null)
        {
            CheckForValidBarClient();
            return await _barClient.Channels.GetAsync(classification);
        }

        /// <summary>
        /// Creates a new channel in the Build Asset Registry
        /// </summary>
        /// <param name="name">Name of channel</param>
        /// <param name="classification">Classification of the channel</param>
        /// <returns>Newly created channel</returns>
        public async Task<Channel> CreateChannelAsync(string name, string classification)
        {
            CheckForValidBarClient();
            return await _barClient.Channels.CreateChannelAsync(name, classification);
        }

        public async Task<IEnumerable<Subscription>> GetSubscriptionsAsync(string sourceRepo = null, string targetRepo = null, int? channelId = null)
        {
            CheckForValidBarClient();
            return await _barClient.Subscriptions.GetAllSubscriptionsAsync(sourceRepo, targetRepo, channelId);
        }

        public async Task<Subscription> GetSubscriptionAsync(string subscriptionId)
        {
            CheckForValidBarClient();
            Guid subscriptionGuid;
            if (!Guid.TryParse(subscriptionId, out subscriptionGuid))
            {
                throw new ArgumentException($"Subscription id '{subscriptionId}' is not a valid guid.");
            }
            return await _barClient.Subscriptions.GetSubscriptionAsync(subscriptionGuid);
        }

        public async Task<Subscription> CreateSubscriptionAsync(string channelName, string sourceRepo, string targetRepo, string targetBranch, string updateFrequency, string mergePolicy)
        {
            CheckForValidBarClient();
            SubscriptionData subscriptionData = new SubscriptionData()
            {
                ChannelName = channelName,
                SourceRepository = sourceRepo,
                TargetRepository = targetRepo,
                TargetBranch = targetBranch,
                Policy = new SubscriptionPolicy()
                {
                    UpdateFrequency = updateFrequency,
                    MergePolicy = mergePolicy
                }
            };
            return await _barClient.Subscriptions.CreateAsync(subscriptionData);
        }

        public async Task<string> CreatePullRequestAsync(string repoUri, string branch, string assetsProducedInCommit, IEnumerable<Microsoft.DotNet.DarcLib.AssetData> assets, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null)
        {
            CheckForValidGitClient();
            _logger.LogInformation($"Create pull request to update dependencies in repo '{repoUri}' and branch '{branch}'...");

            IEnumerable<DependencyDetail> itemsToUpdate = await GetRequiredUpdatesAsync(repoUri, branch, assetsProducedInCommit, assets);

            string linkToPr = null;

            if (itemsToUpdate.Any())
            {
                pullRequestBaseBranch = pullRequestBaseBranch ?? $"darc-{branch}-{Guid.NewGuid()}"; // Base branch must be unique because darc could have multiple PRs open in the same repo at the same time

                await _gitClient.CreateBranchAsync(repoUri, pullRequestBaseBranch, branch);

                await CommitFilesForPullRequestAsync(repoUri, branch, assetsProducedInCommit, itemsToUpdate, pullRequestBaseBranch);

                linkToPr = await _gitClient.CreatePullRequestAsync(repoUri, branch, pullRequestBaseBranch, pullRequestTitle, pullRequestDescription);

                _logger.LogInformation($"Updating dependencies in repo '{repoUri}' and branch '{branch}' succeeded! PR link is: {linkToPr}");

                return linkToPr;
            }

            return linkToPr;
        }

        public async Task<IList<Check>> GetPullRequestChecksAsync(string pullRequestUrl)
        {
            CheckForValidGitClient();
            _logger.LogInformation($"Getting status checks for pull request '{pullRequestUrl}'...");

            IList<Check> checks = await _gitClient.GetPullRequestChecksAsync(pullRequestUrl);

            return checks;
        }

        public async Task<IEnumerable<int>> SearchPullRequestsAsync(string repoUri, string pullRequestBranch, PrStatus status, string keyword = null, string author = null)
        {
            CheckForValidGitClient();
            return await _gitClient.SearchPullRequestsAsync(repoUri, pullRequestBranch, status, keyword, author);
        }

        public Task<IList<Commit>> GetPullRequestCommitsAsync(string pullRequestUrl)
        {
            CheckForValidGitClient();
            return _gitClient.GetPullRequestCommitsAsync(pullRequestUrl);
        }

        public Task<string> CreatePullRequestCommentAsync(string pullRequestUrl, string message)
        {
            CheckForValidGitClient();
            return _gitClient.CreatePullRequestCommentAsync(pullRequestUrl, message);
        }

        public Task UpdatePullRequestCommentAsync(string pullRequestUrl, string commentId, string message)
        {
            CheckForValidGitClient();
            return _gitClient.UpdatePullRequestCommentAsync(pullRequestUrl, commentId, message);
        }

        public async Task<PrStatus> GetPullRequestStatusAsync(string pullRequestUrl)
        {
            CheckForValidGitClient();
            _logger.LogInformation($"Getting the status of pull request '{pullRequestUrl}'...");

            PrStatus status = await _gitClient.GetPullRequestStatusAsync(pullRequestUrl);

            _logger.LogInformation($"Status of pull request '{pullRequestUrl}' is '{status}'");

            return status;
        }

        public async Task<string> UpdatePullRequestAsync(string pullRequestUrl, string assetsProducedInCommit, string branch, IEnumerable<Microsoft.DotNet.DarcLib.AssetData> assetsToUpdate, string pullRequestTitle = null, string pullRequestDescription = null)
        {
            CheckForValidGitClient();
            _logger.LogInformation($"Updating pull request '{pullRequestUrl}'...");

            string linkToPr = null;

            string repoUri = await _gitClient.GetPullRequestRepo(pullRequestUrl);
            string pullRequestBaseBranch = await _gitClient.GetPullRequestBaseBranch(pullRequestUrl);

            IEnumerable<DependencyDetail> itemsToUpdate = await GetRequiredUpdatesAsync(repoUri, branch, assetsProducedInCommit, assetsToUpdate);

            await CommitFilesForPullRequestAsync(repoUri, branch, assetsProducedInCommit, itemsToUpdate, pullRequestBaseBranch);

            linkToPr = await _gitClient.UpdatePullRequestAsync(pullRequestUrl, branch, pullRequestBaseBranch, pullRequestTitle, pullRequestDescription);

            _logger.LogInformation($"Updating dependencies in repo '{repoUri}' and branch '{branch}' succeeded! PR link is: {linkToPr}");

            return linkToPr;
        }

        public async Task MergePullRequestAsync(string pullRequestUrl, MergePullRequestParameters parameters)
        {
            CheckForValidGitClient();
            _logger.LogInformation($"Merging pull request '{pullRequestUrl}'...");

            await _gitClient.MergePullRequestAsync(pullRequestUrl, parameters ?? new MergePullRequestParameters());

            _logger.LogInformation($"Merging pull request '{pullRequestUrl}' succeeded!");
        }

        /// <summary>
        /// Called prior to operations requiring the BAR.  Throws if a bar client isn't available.
        /// </summary>
        private void CheckForValidBarClient()
        {
            if (_barClient == null)
            {
                throw new ArgumentException("Must supply a build asset registry password");
            }
        }

        /// <summary>
        /// Called prior to operations requiring the BAR.  Throws if a git client isn't available;
        /// </summary>
        private void CheckForValidGitClient()
        {
            if (_gitClient == null)
            {
                throw new ArgumentException($"Must supply a valid GitHub/Azure DevOps PAT");
            }
        }

        private void ValidateSettings(DarcSettings settings)
        {
            // Should have a git repo type of AzureDevOps, GitHub, or None.
            if (settings.GitType == GitRepoType.GitHub || settings.GitType == GitRepoType.AzureDevOps)
            {
                // PAT is required for these types.
                if (string.IsNullOrEmpty(settings.PersonalAccessToken))
                {
                    throw new ArgumentException("The personal access token is missing...");
                }
            }
            else if (settings.GitType != GitRepoType.None)
            {
                throw new ArgumentException($"Unexpected git repo type: {settings.GitType}");
            }
        }

        private async Task<IEnumerable<DependencyDetail>> GetRequiredUpdatesAsync(string repoUri, string branch, string assetsProducedInCommit, IEnumerable<Microsoft.DotNet.DarcLib.AssetData> assets)
        {
            CheckForValidGitClient();
            _logger.LogInformation($"Check if repo '{repoUri}' and branch '{branch}' needs updates...");

            List<DependencyDetail> toUpdate = new List<DependencyDetail>();
            IEnumerable<DependencyDetail> dependencyDetails = await _fileManager.ParseVersionDetailsXmlAsync(repoUri, branch);

            foreach (DependencyDetail dependency in dependencyDetails)
            {
                Microsoft.DotNet.DarcLib.AssetData asset = assets.Where(a => a.Name == dependency.Name).FirstOrDefault();

                if (asset == null)
                {
                    _logger.LogInformation($"Dependency '{dependency.Name}' not found in the updated assets...");
                    continue;
                }

                dependency.Version = asset.Version;
                dependency.Commit = assetsProducedInCommit;
                toUpdate.Add(dependency);
            }

            _logger.LogInformation($"Getting dependencies which need to be updated in repo '{repoUri}' and branch '{branch}' succeeded!");

            return toUpdate;
        }

        private async Task<List<GitFile>> GetScriptFilesAsync(string repoUri, string commit)
        {
            CheckForValidGitClient();
            _logger.LogInformation($"Generating commits for script files");

            List<GitFile> files = await _gitClient.GetFilesForCommitAsync(repoUri, commit, "eng/common");

            _logger.LogInformation($"Generating commits for script files succeeded!");

            return files;
        }

        private async Task CommitFilesForPullRequestAsync(string repoUri, string branch, string assetsProducedInCommit, IEnumerable<DependencyDetail> itemsToUpdate, string pullRequestBaseBranch = null)
        {
            CheckForValidGitClient();
            GitFileContentContainer fileContainer = await _fileManager.UpdateDependencyFiles(itemsToUpdate, repoUri, branch);
            List<GitFile> filesToCommit = fileContainer.GetFilesToCommitMap(pullRequestBaseBranch);

            // If there is an arcade asset that we need to update we try to update the script files as well
            DependencyDetail arcadeItem = itemsToUpdate.Where(i => i.Name.ToLower().Contains("arcade")).FirstOrDefault();

            if (arcadeItem != null
                && repoUri != arcadeItem.RepoUri)
            {
                List<GitFile> engCommonsFiles = await GetScriptFilesAsync(arcadeItem.RepoUri, assetsProducedInCommit);
                filesToCommit.AddRange(engCommonsFiles);
            }

            await _gitClient.PushFilesAsync(filesToCommit, repoUri, pullRequestBaseBranch, "Updating version files");
        }
    }
}
