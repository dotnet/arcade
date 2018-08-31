// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    public class Remote : IRemote
    {
        private readonly BuildAssetRegistryClient _barClient;
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
            else
            {
                _gitClient = new VstsClient(settings.PersonalAccessToken, _logger);
            }

            _fileManager = new GitFileManager(_gitClient, _logger);
            _barClient = new BuildAssetRegistryClient(settings.BuildAssetRegistryBaseUri, _logger);
        }

        public async Task<string> CreateChannelAsync(string name, string classification, string barPassword)
        {
            if (string.IsNullOrEmpty(barPassword))
            {
                throw new ArgumentException("A B.A.R password is missing...");
            }

            return await _barClient.CreateChannelAsync(name, classification, barPassword);
        }

        public async Task<string> GetSubscriptionsAsync(string barPassword, string sourceRepo = null, string targetRepo = null, int? channelId = null)
        {
            if (string.IsNullOrEmpty(barPassword))
            {
                throw new ArgumentException("A B.A.R password is missing...");
            }

            return await _barClient.GetSubscriptionsAsync(barPassword, sourceRepo, targetRepo, channelId);
        }

        public async Task<string> GetSubscriptionAsync(int subscriptionId, string barPassword)
        {
            if (string.IsNullOrEmpty(barPassword))
            {
                throw new ArgumentException("A B.A.R password is missing...");
            }

            return await _barClient.GetSubscriptionAsync(subscriptionId, barPassword);
        }

        public async Task<string> CreateSubscriptionAsync(string channelName, string sourceRepo, string targetRepo, string targetBranch, string updateFrequency, string mergePolicy, string barPassword)
        {
            if (string.IsNullOrEmpty(barPassword))
            {
                throw new ArgumentException("A B.A.R password is missing...");
            }

            return await _barClient.CreateSubscriptionAsync(channelName, sourceRepo, targetRepo, targetBranch, updateFrequency, mergePolicy, barPassword);
        }

        public async Task<string> CreatePullRequestAsync(string repoUri, string branch, string assetsProducedInCommit, IEnumerable<AssetData> assets, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null)
        {
            _logger.LogInformation($"Create pull request to update dependencies in repo '{repoUri}' and branch '{branch}'...");

            IEnumerable<DependencyDetail> itemsToUpdate = await GetRequiredUpdatesAsync(repoUri, branch, assetsProducedInCommit, assets);

            string linkToPr = null;

            if (itemsToUpdate.Any())
            {
                pullRequestBaseBranch = pullRequestBaseBranch ?? $"darc-{branch}-{Guid.NewGuid()}"; // Base branch must be unique because darc could have multiple PRs open in the same repo at the same time

                await _gitClient.CreateBranchAsync(repoUri, pullRequestBaseBranch, branch);

                await CommitFilesForPullRequest(repoUri, branch, assetsProducedInCommit, itemsToUpdate, pullRequestBaseBranch);

                linkToPr = await _gitClient.CreatePullRequestAsync(repoUri, branch, pullRequestBaseBranch, pullRequestTitle, pullRequestDescription);

                _logger.LogInformation($"Updating dependencies in repo '{repoUri}' and branch '{branch}' succeeded! PR link is: {linkToPr}");

                return linkToPr;
            }

            return linkToPr;
        }

        public async Task<IList<Check>> GetPullRequestChecksAsync(string pullRequestUrl)
        {
            _logger.LogInformation($"Getting status checks for pull request '{pullRequestUrl}'...");

            IList<Check> checks = await _gitClient.GetPullRequestChecksAsync(pullRequestUrl);

            return checks;
        }

        public async Task<IEnumerable<int>> SearchPullRequestsAsync(string repoUri, string pullRequestBranch, PrStatus status, string keyword = null, string author = null)
        {
            return await _gitClient.SearchPullRequestsAsync(repoUri, pullRequestBranch, status, keyword, author);
        }

        public async Task<PrStatus> GetPullRequestStatusAsync(string pullRequestUrl)
        {
            _logger.LogInformation($"Getting the status of pull request '{pullRequestUrl}'...");

            PrStatus status = await _gitClient.GetPullRequestStatusAsync(pullRequestUrl);

            _logger.LogInformation($"Status of pull request '{pullRequestUrl}' is '{status}'");

            return status;
        }

        public async Task<string> UpdatePullRequestAsync(string pullRequestUrl, string assetsProducedInCommit, string branch, IEnumerable<AssetData> assetsToUpdate, string pullRequestTitle = null, string pullRequestDescription = null)
        {
            _logger.LogInformation($"Updating pull request '{pullRequestUrl}'...");

            string linkToPr = null;

            string repoUri = await _gitClient.GetPullRequestRepo(pullRequestUrl);
            string pullRequestBaseBranch = await _gitClient.GetPullRequestBaseBranch(pullRequestUrl);

            IEnumerable<DependencyDetail> itemsToUpdate = await GetRequiredUpdatesAsync(repoUri, branch, assetsProducedInCommit, assetsToUpdate);

            await CommitFilesForPullRequest(repoUri, branch, assetsProducedInCommit, itemsToUpdate, pullRequestBaseBranch);

            linkToPr = await _gitClient.UpdatePullRequestAsync(pullRequestUrl, branch, pullRequestBaseBranch, pullRequestTitle, pullRequestDescription);

            _logger.LogInformation($"Updating dependencies in repo '{repoUri}' and branch '{branch}' succeeded! PR link is: {linkToPr}");

            return linkToPr;
        }

        public async Task MergePullRequestAsync(string pullRequestUrl, string commit, string mergeMethod, string title, string message)
        {
            _logger.LogInformation($"Merging pull request '{pullRequestUrl}'...");

            await _gitClient.MergePullRequestAsync(pullRequestUrl, commit, mergeMethod, title, message);

            _logger.LogInformation($"Merging pull request '{pullRequestUrl}' succeeded!");
        }

        public async Task CommentOnPullRequestAsync(string repoUri, int pullRequestId, string message)
        {
            _logger.LogInformation($"Adding a comment to PR '{pullRequestId}' in repo '{repoUri}'...");

            await _gitClient.CommentOnPullRequestAsync(repoUri, pullRequestId, message);

            _logger.LogInformation($"Adding a comment to PR '{pullRequestId}' in repo '{repoUri}' succeeded!");
        }

        private void ValidateSettings(DarcSettings settings)
        {
            if (string.IsNullOrEmpty(settings.PersonalAccessToken))
            {
                throw new ArgumentException("The personal access token is missing...");
            }
        }

        private async Task<IEnumerable<DependencyDetail>> GetRequiredUpdatesAsync(string repoUri, string branch, string assetsProducedInCommit, IEnumerable<AssetData> assets)
        {
            _logger.LogInformation($"Check if repo '{repoUri}' and branch '{branch}' needs updates...");

            List<DependencyDetail> toUpdate = new List<DependencyDetail>();
            IEnumerable<DependencyDetail> dependencyDetails = await _fileManager.ParseVersionDetailsXmlAsync(repoUri, branch);

            foreach (DependencyDetail dependency in dependencyDetails)
            {
                AssetData asset = assets.Where(a => a.Name == dependency.Name).FirstOrDefault();

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

        private async Task<List<GitFile>> GetScriptCommitsAsync(string repoUri, string branch, string assetsProducedInCommit, string pullRequestBaseBranch)
        {
            _logger.LogInformation($"Generating commits for script files");

            List<GitFile> commits = await _gitClient.GetCommitsForPathAsync(repoUri, branch, assetsProducedInCommit, pullRequestBaseBranch);

            _logger.LogInformation($"Generating commits for script files succeeded!");

            return commits;
        }

        private async Task CommitFilesForPullRequest(string repoUri, string branch, string assetsProducedInCommit, IEnumerable<DependencyDetail> itemsToUpdate, string pullRequestBaseBranch = null)
        {
            GitFileContentContainer fileContainer = await _fileManager.UpdateDependencyFiles(itemsToUpdate, repoUri, branch);
            List<GitFile> arcadeFiles= fileContainer.GetFilesToCommitMap(pullRequestBaseBranch);

            // If there is an arcade asset that we need to update we try to update the script files as well
            DependencyDetail arcadeItem = itemsToUpdate.Where(i => i.Name.ToLower().Contains("arcade")).FirstOrDefault();

            if (arcadeItem != null)
            {
                List<GitFile> engCommonsFiles = await GetScriptCommitsAsync(repoUri, branch, assetsProducedInCommit, pullRequestBaseBranch);
                arcadeFiles.AddRange(engCommonsFiles);
            }

            await _gitClient.PushCommitsAsync(arcadeFiles, repoUri, pullRequestBaseBranch, "Updating version files");
        }
    }
}
