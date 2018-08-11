using System.Threading.Tasks;
using Maestro.GitHub;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DependencyUpdater
{
    public class DarcRemoteFactory : IDarcRemoteFactory
    {
        public ILoggerFactory LoggerFactory { get; }
        public IConfigurationRoot Configuration { get; }
        public IGitHubTokenProvider GitHubTokenProvider { get; }

        public DarcRemoteFactory(ILoggerFactory loggerFactory, IConfigurationRoot configuration, IGitHubTokenProvider gitHubTokenProvider)
        {
            LoggerFactory = loggerFactory;
            Configuration = configuration;
            GitHubTokenProvider = gitHubTokenProvider;
        }

        public async Task<IRemote> CreateAsync(string repoUrl, long installationId)
        {
            var settings = new DarcSettings();
            if (repoUrl.Contains("github.com"))
            {
                settings.GitType = GitRepoType.GitHub;
                settings.PersonalAccessToken = await GitHubTokenProvider.GetTokenForInstallation(installationId);

            }
            else
            {
                settings.GitType = GitRepoType.Vsts;
                settings.PersonalAccessToken = ""; // TODO: get this
            }

            return new Remote(settings, LoggerFactory.CreateLogger<Remote>());
        }
    }
}