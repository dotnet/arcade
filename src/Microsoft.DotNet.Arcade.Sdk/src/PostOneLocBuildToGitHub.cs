// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Octokit;
using Octokit.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Arcade.Sdk
{
    public class PostOneLocBuildToGitHub : MSBuildTaskBase
    {
        [Required]
        public string LocFilesDirectory { get; set; }

        [Required]
        public string SourcesDirectory { get; set; }

        [Required]
        public string GitHubPat { get; set; }

        [Required]
        public string GitHubSourceOrg { get; set; }

        [Required]
        public string GitHubTargetOrg { get; set; }

        [Required]
        public string GitHubRepo { get; set; }

        [Required]
        public string GitHubBranch { get; set; }

        public static string PrDiffFileName = "BinPlaceFileList.txt";
        public static string PrPrefix = "Add localization from OneLocBuild ";

        private const string _gitFileBlobMode = "100644";

        public override void ConfigureServices(IServiceCollection collection)
        {
            collection.TryAddSingleton<IGitHubClient>(new GitHubClient(new ProductHeaderValue("OneLocBuild")));
            collection.TryAddSingleton<IFileSystem, FileSystem>();
            collection.TryAddSingleton<IHelpers, Helpers>();
            collection.TryAddSingleton(Log);
        }

        public bool ExecuteTask(IGitHubClient gitHubClient, IFileSystem fileSystem, IHelpers helpers)
        {
            return ExecuteAsync(gitHubClient, fileSystem, helpers).GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync(IGitHubClient gitHubClient, IFileSystem fileSystem, IHelpers helpers)
        {
            gitHubClient.Connection.Credentials = new Credentials("dnbot", GitHubPat);

            string pathToPrDiffFile = Path.Combine(LocFilesDirectory, PrDiffFileName);
            if (!fileSystem.FileExists(pathToPrDiffFile))
            {
                Log.LogError($"PR diff file '{pathToPrDiffFile}' not found.");
                return false;
            }

            string[] filesToPr = fileSystem.ReadFromFile(pathToPrDiffFile).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (filesToPr.Length == 0)
            {
                Log.LogMessage($"No loc changes. Exiting.");
                return true;
            }

            Reference targetBranch = await gitHubClient.Git.Reference.Get(GitHubTargetOrg, GitHubRepo, $"heads/{GitHubBranch}");
            Reference newBranch;
            string branchName;
            string branchRef;

            // Find out if a PR already exists; if it does, use its branch
            PullRequest existingPr = await FindExistingOneLocPr(gitHubClient);
            if (existingPr is not null)
            {
                branchName = existingPr.Head.Ref;
                branchRef = $"heads/{branchName}";
                newBranch = await gitHubClient.Git.Reference.Get(GitHubSourceOrg, GitHubRepo, branchRef);
            }
            // If not, we create a new branch
            else
            {
                branchName = $"OneLocBuild-{Guid.NewGuid()}";
                branchRef = $"heads/{branchName}";
                newBranch = await gitHubClient.Git.Reference.CreateBranch(GitHubSourceOrg, GitHubRepo, branchName, targetBranch);
            }

            TreeResponse currentTree = await gitHubClient.Git.Tree.Get(GitHubSourceOrg, GitHubRepo, branchRef);
            NewTree newTree = new NewTree
            {
                BaseTree = currentTree.Sha
            };

            // Add each file to the tree
            foreach (string file in filesToPr)
            {
                string filePath = file.Substring(SourcesDirectory.Length).Replace('\\', '/'); // equivalent to Path.GetRelativePath() but converts to git path format
                string locdFilePath = fileSystem.GetFiles(LocFilesDirectory, Path.GetFileName(file), SearchOption.AllDirectories).First();
                string locdFileContent = fileSystem.ReadFromFile(locdFilePath);

                NewTreeItem locdFile = new NewTreeItem
                {
                    Path = filePath,
                    Mode = _gitFileBlobMode,
                    Type = TreeType.Blob,
                    Content = locdFileContent,
                };
                newTree.Tree.Add(locdFile);
            }

            TreeResponse treeResponse = await gitHubClient.Git.Tree.Create(GitHubSourceOrg, GitHubRepo, newTree);

            // Create a commit
            NewCommit newCommit = new NewCommit($"{PrPrefix}({DateTimeOffset.Now:yyyy-MM-dd})",
                treeResponse.Sha,
                newBranch.Object.Sha);
            Commit commit = await gitHubClient.Git.Commit.Create(GitHubSourceOrg, GitHubRepo, newCommit);

            ReferenceUpdate update = new ReferenceUpdate(commit.Sha);
            await gitHubClient.Git.Reference.Update(GitHubSourceOrg, GitHubRepo, branchRef, update);

            // Create a new pull request if one does not exist
            if (existingPr is null)
            {
                NewPullRequest newPr = new NewPullRequest(newCommit.Message, $"{GitHubSourceOrg}:{branchName}", GitHubBranch);
                await gitHubClient.PullRequest.Create(GitHubTargetOrg, GitHubRepo, newPr);
            }

            return true;
        }

        public async Task<PullRequest> FindExistingOneLocPr(IGitHubClient gitHubClient)
        {
            PullRequestRequest filter = new PullRequestRequest { State = ItemStateFilter.Open };
            var prs = (await gitHubClient.PullRequest.GetAllForRepository(GitHubTargetOrg, GitHubRepo, filter))
                .Where(pr => pr.Title.StartsWith(PrPrefix))
                .Where(pr => pr.Base.Label.Equals($"{GitHubTargetOrg}:{GitHubBranch}", StringComparison.OrdinalIgnoreCase));

            if (prs.Count() > 0)
            {
                return prs.First();
            }
            else
            {
                return null;
            }
        }
    }
}
