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
        /// Retrieve a list of default channel associations.
        /// </summary>
        /// <param name="repository">Optionally filter by repository</param>
        /// <param name="branch">Optionally filter by branch</param>
        /// <param name="channelId">Optionally filter by channel ID</param>
        /// <returns>Collection of default channels.</returns>
        public async Task<IEnumerable<DefaultChannel>> GetDefaultChannelsAsync(string repository = null, string branch = null, int? channelId = null)
        {
            CheckForValidBarClient();
            return await _barClient.DefaultChannels.ListAsync(repository, branch, channelId);
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
            if (!Guid.TryParse(subscriptionId, out Guid subscriptionGuid))
            {
                throw new ArgumentException($"Subscription id '{subscriptionId}' is not a valid guid.");
            }
            return await _barClient.Subscriptions.GetSubscriptionAsync(subscriptionGuid);
        }

        /// <summary>
        /// Create a new subscription
        /// </summary>
        /// <param name="channelName">Name of source channel</param>
        /// <param name="sourceRepo">URL of source repository</param>
        /// <param name="targetRepo">URL of target repository where updates should be made</param>
        /// <param name="targetBranch">Name of target branch where updates should be made</param>
        /// <param name="updateFrequency">Frequency of updates, can be 'none', 'everyBuild' or 'everyDay'</param>
        /// <param name="mergePolicies">Dictionary of merge policies. Each merge policy is a name of a policy with an associated blob
        /// of metadata</param>
        /// <returns>Newly created subscription, if successful</returns>
        public async Task<Subscription> CreateSubscriptionAsync(string channelName, string sourceRepo, string targetRepo,
            string targetBranch, string updateFrequency, List<MergePolicy> mergePolicies)
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
                    MergePolicies = mergePolicies
                }
            };
            return await _barClient.Subscriptions.CreateAsync(subscriptionData);
        }

        /// <summary>
        /// Delete a subscription by id
        /// </summary>
        /// <param name="subscriptionId">Id of subscription to delete</param>
        /// <returns>Information on deleted subscriptio</returns>
        public async Task<Subscription> DeleteSubscriptionAsync(string subscriptionId)
        {
            CheckForValidBarClient();
            if (!Guid.TryParse(subscriptionId, out Guid subscriptionGuid))
            {
                throw new ArgumentException($"Subscription id '{subscriptionId}' is not a valid guid.");
            }
            return await _barClient.Subscriptions.DeleteSubscriptionAsync(subscriptionGuid);
        }

        public async Task CreateNewBranchAsync(string repoUri, string baseBranch, string newBranch)
        {
            await _gitClient.CreateBranchAsync(repoUri, newBranch, baseBranch);
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
            return _gitClient.GetPullRequestCommitsAsync(pullRequestUrl);
        }

        public Task CreateOrUpdatePullRequestDarcCommentAsync(string pullRequestUrl, string message)
        {
            CheckForValidGitClient();
            return _gitClient.CreateOrUpdatePullRequestDarcCommentAsync(pullRequestUrl, message);
        }

        public async Task<PrStatus> GetPullRequestStatusAsync(string pullRequestUrl)
        {
            CheckForValidGitClient();
            _logger.LogInformation($"Getting the status of pull request '{pullRequestUrl}'...");

            PrStatus status = await _gitClient.GetPullRequestStatusAsync(pullRequestUrl);

            _logger.LogInformation($"Status of pull request '{pullRequestUrl}' is '{status}'");

            return status;
        }

        public Task UpdatePullRequestAsync(string pullRequestUri, PullRequest pullRequest)
        {
            return _gitClient.UpdatePullRequestAsync(pullRequestUri, pullRequest);
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

        public async Task<List<DependencyDetail>> GetRequiredUpdatesAsync(string repoUri, string branch, string sourceCommit, IEnumerable<AssetData> assets)
        {
            CheckForValidGitClient();
            _logger.LogInformation($"Check if repo '{repoUri}' and branch '{branch}' needs updates...");

            var toUpdate = new List<DependencyDetail>();
            IEnumerable<DependencyDetail> dependencyDetails = await _fileManager.ParseVersionDetailsXmlAsync(repoUri, branch);
            Dictionary<string, DependencyDetail> dependencies = dependencyDetails.ToDictionary(d => d.Name);

            foreach (AssetData asset in assets)
            {
                if (!dependencies.TryGetValue(asset.Name, out DependencyDetail dependency))

                {
                    _logger.LogInformation($"No dependency found for updated asset '{asset.Name}'");
                    continue;
                }

                dependency.Version = asset.Version;
                dependency.Commit = sourceCommit;
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

        public async Task CommitUpdatesAsync(
            string repoUri,
            string branch,
            List<DependencyDetail> itemsToUpdate,
            string message)
        {
            CheckForValidGitClient();
            GitFileContentContainer fileContainer = await _fileManager.UpdateDependencyFiles(itemsToUpdate, repoUri, branch);
            List<GitFile> filesToCommit = fileContainer.GetFilesToCommit();

            // If we are updating the arcade sdk we need to update the eng/common files as well
            DependencyDetail arcadeItem = itemsToUpdate.FirstOrDefault(
                i => string.Equals(i.Name, "Microsoft.DotNet.Arcade.Sdk", StringComparison.OrdinalIgnoreCase));

            if (arcadeItem != null
                && repoUri != arcadeItem.RepoUri)
            {
                List<GitFile> engCommonFiles = await GetScriptFilesAsync(arcadeItem.RepoUri, arcadeItem.Commit);
                filesToCommit.AddRange(engCommonFiles);
            }

            await _gitClient.PushFilesAsync(filesToCommit, repoUri, branch, message);
        }

        public Task<PullRequest> GetPullRequestAsync(string pullRequestUri)
        {
            return _gitClient.GetPullRequestAsync(pullRequestUri);
        }

        public Task<string> CreatePullRequestAsync(string repoUri, PullRequest pullRequest)
        {
            return _gitClient.CreatePullRequestAsync(repoUri, pullRequest);
        }
    }
}
