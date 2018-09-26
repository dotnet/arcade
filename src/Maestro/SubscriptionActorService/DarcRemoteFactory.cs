// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Maestro.GitHub;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SubscriptionActorService
{
    public class DarcRemoteFactory : IDarcRemoteFactory
    {
        public DarcRemoteFactory(
            ILoggerFactory loggerFactory,
            IConfigurationRoot configuration,
            IGitHubTokenProvider gitHubTokenProvider)
        {
            LoggerFactory = loggerFactory;
            Configuration = configuration;
            GitHubTokenProvider = gitHubTokenProvider;
        }

        public ILoggerFactory LoggerFactory { get; }
        public IConfigurationRoot Configuration { get; }
        public IGitHubTokenProvider GitHubTokenProvider { get; }

        public async Task<IRemote> CreateAsync(string repoUrl, long installationId)
        {
            var settings = new DarcSettings();
            if (repoUrl.Contains("github.com"))
            {
                if (installationId == default)
                {
                    throw new SubscriptionException($"No installation is avaliable for repository '{repoUrl}'");
                }

                settings.GitType = GitRepoType.GitHub;
                settings.PersonalAccessToken = await GitHubTokenProvider.GetTokenForInstallation(installationId);
            }
            else
            {
                settings.GitType = GitRepoType.AzureDevOps;
                settings.PersonalAccessToken = ""; // TODO: get this
            }

            return new Remote(settings, LoggerFactory.CreateLogger<Remote>());
        }
    }
}
