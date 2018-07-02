using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc
{
    public class RemoteActions : IRemote
    {
        private readonly DependencyFileManager fileManager;

        private readonly IGitRepo gitClient;

        public RemoteActions(DarcSettings settings)
        {
            ValidateSettings(settings);

            if (settings.GitType == GitRepoType.GitHub)
            {
                gitClient = new GitHubClient(settings.PersonalAccessToken);
            }
            else
            {
                gitClient = new VstsClient(settings.PersonalAccessToken);
            }

            fileManager = new DependencyFileManager(gitClient);
        }

        public Task<IEnumerable<BuildAsset>> GetDependantAssetsAsync(string assetName, string version = null, string repoUri = null, string branch = null, string sha = null, DependencyType type = DependencyType.Unknown)
        {
            // TODO: call Build Asset Registry APIs
            throw new NotImplementedException();
        }

        public Task<IEnumerable<BuildAsset>> GetDependencyAssetsAsync(string assetName, string version = null, string repoUri = null, string branch = null, string sha = null, DependencyType type = DependencyType.Unknown)
        {
            // TODO: call Build Asset Registry APIs
            throw new NotImplementedException();
        }

        public Task<BuildAsset> GetLatestDependencyAsync(string assetName)
        {
            List<BuildAsset> dependencies = new List<BuildAsset>();

            Console.WriteLine($"Getting latest dependency version for '{assetName}' in the reporting store...");

            assetName = assetName.Replace('*', '%').Replace('?', '%');

            // TODO: call Build Asset Registry APIs

            return Task.Run(() => dependencies.FirstOrDefault());
        }

        public async Task<IEnumerable<BuildAsset>> GetRequiredUpdatesAsync(string repoUri, string branch)
        {
            Console.WriteLine($"Getting dependencies which need to be updated in repo '{repoUri}' and branch '{branch}'...");

            List<BuildAsset> toUpdate = new List<BuildAsset>();
            IEnumerable<BuildAsset> dependencies = await fileManager.ParseVersionDetailsXmlAsync(repoUri, branch);

            foreach (BuildAsset BuildAsset in dependencies)
            {
                BuildAsset latest = await GetLatestDependencyAsync(BuildAsset.Name);

                if (latest != null)
                {
                    if (string.Compare(latest.Version, BuildAsset.Version) == 1)
                    {
                        BuildAsset.Version = latest.Version;
                        BuildAsset.Sha = latest.Sha;
                        toUpdate.Add(BuildAsset);
                    }
                }
                else
                {
                    Console.WriteLine($"No asset with name '{BuildAsset.Name}' found in store but it is defined in repo '{repoUri}' and branch '{branch}'.");
                }
            }

            Console.WriteLine($"Getting dependencies which need to be updated in repo '{repoUri}' and branch '{branch}' succeeded!");

            return toUpdate;
        }

        public async Task<string> CreatePullRequestAsync(IEnumerable<BuildAsset> itemsToUpdate, string repoUri, string branch, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null)
        {
            Console.WriteLine($"Create pull request to update dependencies in repo '{repoUri}' and branch '{branch}'...");

            string linkToPr = null;

            await gitClient.CreateDarcBranchAsync(repoUri, branch);

            pullRequestBaseBranch = pullRequestBaseBranch ?? $"darc-{branch}";

            // Check for exsting PRs in the darc created branch. If there is one under the same user we fail fast before commiting files that won't be included in a PR. 
            string existingPr = await gitClient.CheckForOpenPullRequestsAsync(repoUri, pullRequestBaseBranch);

            if (string.IsNullOrEmpty(existingPr))
            {
                await CommitFilesForPullRequest(itemsToUpdate, repoUri, branch, pullRequestBaseBranch, pullRequestTitle, pullRequestDescription);

                linkToPr = await gitClient.CreatePullRequestAsync(repoUri, branch, pullRequestBaseBranch, pullRequestTitle, pullRequestDescription);

                Console.WriteLine($"Updating dependencies in repo '{repoUri}' and branch '{branch}' succeeded! PR link is: {linkToPr}");

                return linkToPr;
            }

            Console.WriteLine($"PR with link '{existingPr}' is already opened in repo '{repoUri}' and branch '{pullRequestBaseBranch}' please update it instead of trying to create a new one");

            return linkToPr;
        }

        public async Task<string> UpdatePullRequestAsync(IEnumerable<BuildAsset> itemsToUpdate, string repoUri, string branch, int pullRequestId, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null)
        {
            Console.WriteLine($"Updating pull request '{pullRequestId}' in repo '{repoUri}' and branch '{branch}'...");
            string linkToPr = null;

            pullRequestBaseBranch = pullRequestBaseBranch ?? $"darc-{branch}";

            await CommitFilesForPullRequest(itemsToUpdate, repoUri, branch, pullRequestBaseBranch, pullRequestTitle, pullRequestDescription);

            linkToPr = await gitClient.UpdatePullRequestAsync(repoUri, branch, pullRequestBaseBranch, pullRequestId, pullRequestTitle, pullRequestDescription);

            Console.WriteLine($"Updating dependencies in repo '{repoUri}' and branch '{branch}' succeeded! PR link is: {linkToPr}");

            return linkToPr;
        }

        private void ValidateSettings(DarcSettings settings)
        {
            if (string.IsNullOrEmpty(settings.PersonalAccessToken))
            {
                throw new ArgumentException("When using remote actions a personal access token has to be set.");
            }
        }

        private async Task<Dictionary<string, GitCommit>> GetScriptCommitsAsync(string branch, string assetName = "arcade.sdk")
        {
            Console.WriteLine($"Generating commits for script files");

            BuildAsset latestAsset = await GetLatestDependencyAsync(assetName);

            Dictionary<string, GitCommit> commits = await gitClient.GetCommitsForPathAsync(latestAsset.RepoUri, latestAsset.Sha, branch);

            Console.WriteLine($"Generating commits for script files succeeded!");

            return commits;
        }

        private async Task CommitFilesForPullRequest(IEnumerable<BuildAsset> itemsToUpdate, string repoUri, string branch, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null)
        {
            DependencyFileContentContainer fileContainer = await fileManager.UpdateDependencyFiles(itemsToUpdate, repoUri, branch);
            Dictionary<string, GitCommit> dependencyFilesToCommit = fileContainer.GetFilesToCommitMap(pullRequestBaseBranch);

            await gitClient.PushFilesAsync(dependencyFilesToCommit, repoUri, pullRequestBaseBranch);

            // If there is an arcade asset that we need to update we try to update the script files as well
            BuildAsset arcadeItem = itemsToUpdate.Where(i => i.Name.Contains("arcade")).FirstOrDefault();

            if (arcadeItem != null)
            {
                await gitClient.PushFilesAsync(await GetScriptCommitsAsync(branch, assetName: arcadeItem.Name), repoUri, pullRequestBaseBranch);
            }
        }

        private Task<IEnumerable<BuildAsset>> GetAssetsAsync()
        {
            throw new NotImplementedException();
        }
    }
}
