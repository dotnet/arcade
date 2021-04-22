// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Octokit;
using System;
using System.Collections.Generic;
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
        public string GitHubPat { get; set; }

        [Required]
        public string GitHubOrg { get; set; }

        [Required]
        public string GitHubRepo { get; set; }

        public static string PrPrefix = "Localized files from OneLocBuild for ";

        public override void ConfigureServices(IServiceCollection collection)
        {
            collection.TryAddSingleton<IGitHubClient>(new GitHubClient(new ProductHeaderValue("OneLocBuild")));
            collection.TryAddSingleton<IFileSystem, FileSystem>();
            collection.TryAddSingleton<IHelpers, Helpers>();
            collection.TryAddSingleton(Log);
        }

        public bool ExecuteTask(IGitHubClient gitHubClient, IFileSystem fileSystem, IHelpers helpers)
        {
            gitHubClient.Connection.Credentials = new Credentials("dnbot", GitHubPat);



            return true;
        }

        public async Task<PullRequest> FindExistingOneLocPr(IGitHubClient gitHubClient)
        {
            PullRequestRequest filter = new PullRequestRequest { State = ItemStateFilter.Open };
            var prs = (await gitHubClient.PullRequest.GetAllForRepository(GitHubOrg, GitHubRepo, filter))
                .Where(pr => pr.Title.StartsWith(PrPrefix));

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
