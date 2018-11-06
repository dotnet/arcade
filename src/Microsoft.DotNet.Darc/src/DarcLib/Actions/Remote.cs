// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Maestro.Client;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging;

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
                    _barClient = ApiFactory.GetAuthenticated(
                        settings.BuildAssetRegistryBaseUri,
                        settings.BuildAssetRegistryPassword);
                }
                else
                {
                    _barClient = ApiFactory.GetAuthenticated(settings.BuildAssetRegistryPassword);
                }
            }
        }

        /// <summary>
        ///     Retrieve a list of default channel associations.
        /// </summary>
        /// <param name="repository">Optionally filter by repository</param>
        /// <param name="branch">Optionally filter by branch</param>
        /// <param name="channel">Optionally filter by channel</param>
        /// <returns>Collection of default channels.</returns>
        public async Task<IEnumerable<DefaultChannel>> GetDefaultChannelsAsync(
            string repository = null,
            string branch = null,
            string channel = null)
        {
            CheckForValidBarClient();
            IList<DefaultChannel> channels = await _barClient.DefaultChannels.ListAsync(repository, branch);
            if (!string.IsNullOrEmpty(channel))
            {
                return channels.Where(c => c.Channel.Name.Equals(channel, StringComparison.OrdinalIgnoreCase));
            }

            // Filter away based on channel info.
            return channels;
        }

        /// <summary>
        ///     Adds a default channel association.
        /// </summary>
        /// <param name="repository">Repository receiving the default association</param>
        /// <param name="branch">Branch receiving the default association</param>
        /// <param name="channel">Name of channel that builds of 'repository' on 'branch' should automatically be applied to.</param>
        /// <returns>Async task.</returns>
        public async Task AddDefaultChannelAsync(string repository, string branch, string channel)
        {
            CheckForValidBarClient();
            // Look up channel to translate to channel id.
            Channel foundChannel = (await _barClient.Channels.GetAsync())
                .Where(c => c.Name.Equals(channel, StringComparison.OrdinalIgnoreCase))
                .SingleOrDefault();
            if (foundChannel == null)
            {
                throw new ArgumentException($"Channel {channel} is not a valid channel.");
            }

            var defaultChannelsData = new PostData
            {
                Branch = branch,
                Repository = repository,
                ChannelId = foundChannel.Id.Value
            };

            await _barClient.DefaultChannels.CreateAsync(defaultChannelsData);
        }

        /// <summary>
        ///     Removes a default channel based on the specified criteria
        /// </summary>
        /// <param name="repository">Repository having a default association</param>
        /// <param name="branch">Branch having a default association</param>
        /// <param name="channel">Name of channel that builds of 'repository' on 'branch' are being applied to.</param>
        /// <returns>Async task</returns>
        public async Task DeleteDefaultChannelAsync(string repository, string branch, string channel)
        {
            CheckForValidBarClient();

            DefaultChannel existingDefaultChannel =
                (await GetDefaultChannelsAsync(repository, branch, channel)).SingleOrDefault();

            if (existingDefaultChannel != null)
            {
                // Find the existing default channel.  If none found then nothing to do.
                await _barClient.DefaultChannels.DeleteAsync(existingDefaultChannel.Id.Value);
            }
        }

        /// <summary>
        ///     Creates a new channel in the Build Asset Registry
        /// </summary>
        /// <param name="name">Name of channel</param>
        /// <param name="classification">Classification of the channel</param>
        /// <returns>Newly created channel</returns>
        public async Task<Channel> CreateChannelAsync(string name, string classification)
        {
            CheckForValidBarClient();
            return await _barClient.Channels.CreateChannelAsync(name, classification);
        }

        /// <summary>
        /// Deletes a channel from the Build Asset Registry
        /// </summary>
        /// <param name="name">Name of channel</param>
        /// <returns>Channel just deleted</returns>
        public async Task<Channel> DeleteChannelAsync(int id)
        {
            CheckForValidBarClient();
            return await _barClient.Channels.DeleteChannelAsync(id);
        }

        public async Task<IEnumerable<Subscription>> GetSubscriptionsAsync(
            string sourceRepo = null,
            string targetRepo = null,
            int? channelId = null)
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
        ///     Create a new subscription
        /// </summary>
        /// <param name="channelName">Name of source channel</param>
        /// <param name="sourceRepo">URL of source repository</param>
        /// <param name="targetRepo">URL of target repository where updates should be made</param>
        /// <param name="targetBranch">Name of target branch where updates should be made</param>
        /// <param name="updateFrequency">Frequency of updates, can be 'none', 'everyBuild' or 'everyDay'</param>
        /// <param name="mergePolicies">
        ///     Dictionary of merge policies. Each merge policy is a name of a policy with an associated blob
        ///     of metadata
        /// </param>
        /// <returns>Newly created subscription, if successful</returns>
        public async Task<Subscription> CreateSubscriptionAsync(
            string channelName,
            string sourceRepo,
            string targetRepo,
            string targetBranch,
            string updateFrequency,
            List<MergePolicy> mergePolicies)
        {
            CheckForValidBarClient();
            var subscriptionData = new SubscriptionData
            {
                ChannelName = channelName,
                SourceRepository = sourceRepo,
                TargetRepository = targetRepo,
                TargetBranch = targetBranch,
                Policy = new SubscriptionPolicy
                {
                    UpdateFrequency = updateFrequency,
                    MergePolicies = mergePolicies
                }
            };
            return await _barClient.Subscriptions.CreateAsync(subscriptionData);
        }

        /// <summary>
        ///     Delete a subscription by id
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

        /// <summary>
        /// Retrieve subscription history.
        /// </summary>
        /// <param name="subscriptionId">ID of subscription</param>
        /// <returns>Subscription history</returns>
        public async Task<IEnumerable<SubscriptionHistoryItem>> GetSubscriptionHistoryAsync(string subscriptionId)
        {
            CheckForValidBarClient();
            if (!Guid.TryParse(subscriptionId, out Guid subscriptionGuid))
            {
                throw new ArgumentException($"Subscription id '{subscriptionId}' is not a valid guid.");
            }

            return await _barClient.Subscriptions.GetSubscriptionHistoryAsync(subscriptionGuid);
        }

        /// <summary>
        /// Retry subscription operation.
        /// </summary>
        /// <param name="subscriptionId">Id of subscription that should have its action retried</param>
        /// <param name="actionIdentifier">Timestamp of the action that needs to be retried</param>
        public Task RetrySubscriptionUpdateAsync(string subscriptionId, long actionIdentifier)
        {
            CheckForValidBarClient();
            if (!Guid.TryParse(subscriptionId, out Guid subscriptionGuid))
            {
                throw new ArgumentException($"Subscription id '{subscriptionId}' is not a valid guid.");
            }

            return _barClient.Subscriptions.RetrySubscriptionActionAsyncAsync(subscriptionGuid, actionIdentifier);
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

        public async Task<IEnumerable<int>> SearchPullRequestsAsync(
            string repoUri,
            string pullRequestBranch,
            PrStatus status,
            string keyword = null,
            string author = null)
        {
            CheckForValidGitClient();
            return await _gitClient.SearchPullRequestsAsync(repoUri, pullRequestBranch, status, keyword, author);
        }

        public Task<IList<Commit>> GetPullRequestCommitsAsync(string pullRequestUrl)
        {
            return _gitClient.GetPullRequestCommitsAsync(pullRequestUrl);
        }

        public Task CreateOrUpdatePullRequestStatusCommentAsync(string pullRequestUrl, string message)
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

        public async Task<List<DependencyDetail>> GetRequiredUpdatesAsync(
            string repoUri,
            string branch,
            string sourceCommit,
            IEnumerable<AssetData> assets)
        {
            CheckForValidGitClient();
            _logger.LogInformation($"Check if repo '{repoUri}' and branch '{branch}' needs updates...");

            var toUpdate = new List<DependencyDetail>();
            IEnumerable<DependencyDetail> dependencyDetails =
                await _fileManager.ParseVersionDetailsXmlAsync(repoUri, branch);
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

            _logger.LogInformation(
                $"Getting dependencies which need to be updated in repo '{repoUri}' and branch '{branch}' succeeded!");

            return toUpdate;
        }

        public async Task CommitUpdatesAsync(
            string repoUri,
            string branch,
            List<DependencyDetail> itemsToUpdate,
            string message)
        {
            CheckForValidGitClient();
            GitFileContentContainer fileContainer =
                await _fileManager.UpdateDependencyFiles(itemsToUpdate, repoUri, branch);
            List<GitFile> filesToCommit = fileContainer.GetFilesToCommit();

            // If we are updating the arcade sdk we need to update the eng/common files as well
            DependencyDetail arcadeItem = itemsToUpdate.FirstOrDefault(
                i => string.Equals(i.Name, "Microsoft.DotNet.Arcade.Sdk", StringComparison.OrdinalIgnoreCase));

            if (arcadeItem != null && repoUri != arcadeItem.RepoUri)
            {
                // Files in arcade repository
                List<GitFile> engCommonFiles = await GetCommonScriptFilesAsync(arcadeItem.RepoUri, arcadeItem.Commit);
                filesToCommit.AddRange(engCommonFiles);

                // Files in the target repo
                string latestCommit = await _gitClient.GetLastCommitShaAsync(_gitClient.GetOwnerAndRepoFromRepoUri(repoUri), branch);
                List<GitFile> targetEngCommonFiles = await GetCommonScriptFilesAsync(repoUri, latestCommit);

                foreach (GitFile file in targetEngCommonFiles)
                {
                    if (!engCommonFiles.Where(f => f.FilePath == file.FilePath).Any())
                    {
                        file.Operation = GitFileOperation.Delete;
                        filesToCommit.Add(file);
                    }
                }
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

        /// <summary>
        ///     Retrieve the list of channels from the build asset registry.
        /// </summary>
        /// <param name="classification">Optional classification to get</param>
        /// <returns></returns>
        public async Task<IEnumerable<Channel>> GetChannelsAsync(string classification = null)
        {
            CheckForValidBarClient();
            return await _barClient.Channels.GetAsync(classification);
        }

        /// <summary>
        ///     Retrieve a specific channel by name.
        /// </summary>
        /// <param name="channel">Channel name.</param>
        /// <returns>Channel or null if not found.</returns>
        public async Task<Channel> GetChannelAsync(string channel)
        {
            CheckForValidBarClient();
            return (await _barClient.Channels.GetAsync()).Where(c => c.Name.Equals(channel, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
        }

        /// <summary>
        ///     Retrieve the latest build of a repository on a specific channel.
        /// </summary>
        /// <param name="repoUri">URI of repository to obtain a build for.</param>
        /// <param name="channelId">Channel the build was applied to.</param>
        /// <returns>Latest build of <paramref name="repoUri"/> on channel <paramref name="channelId"/>,
        /// or null if there is no latest.</returns>
        /// <remarks>The build's assets are returned</remarks>
        public Task<Build> GetLatestBuildAsync(string repoUri, int channelId)
        {
            CheckForValidBarClient();
            return _barClient.Builds.GetLatestAsync(repository: repoUri, channelId: channelId, loadCollections: true);
        }

        public async Task<IEnumerable<DependencyDetail>> GetDependenciesAsync(string repoUri, string branch, string name = null)
        {
            CheckForValidGitClient();
            return (await _fileManager.ParseVersionDetailsXmlAsync(repoUri, branch)).Where(
                dependency => string.IsNullOrEmpty(name) || dependency.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     Called prior to operations requiring the BAR.  Throws if a bar client isn't available.
        /// </summary>
        private void CheckForValidBarClient()
        {
            if (_barClient == null)
            {
                throw new ArgumentException("Must supply a build asset registry password");
            }
        }

        /// <summary>
        ///     Called prior to operations requiring the BAR.  Throws if a git client isn't available;
        /// </summary>
        private void CheckForValidGitClient()
        {
            if (_gitClient == null)
            {
                throw new ArgumentException("Must supply a valid GitHub/Azure DevOps PAT");
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

        public async Task<List<GitFile>> GetCommonScriptFilesAsync(string repoUri, string commit)
        {
            CheckForValidGitClient();
            _logger.LogInformation("Generating commits for script files");

            List<GitFile> files = await _gitClient.GetFilesForCommitAsync(repoUri, commit, "eng/common");

            _logger.LogInformation("Generating commits for script files succeeded!");

            return files;
        }
    }
}
