using System.Threading.Tasks;
using GitHubJwt;
using Microsoft.Extensions.Options;
using Octokit;

namespace Maestro.GitHub
{
    public class GitHubTokenProvider : IGitHubTokenProvider
    {
        private readonly IOptions<GitHubTokenProviderOptions> _options;
        public GitHubTokenProviderOptions Options => _options.Value;

        public GitHubTokenProvider(IOptions<GitHubTokenProviderOptions> options)
        {
            _options = options;
        }

        private string GetAppToken()
        {
            var generator = new GitHubJwtFactory(
                new StringPrivateKeySource(Options.PrivateKey),
                new GitHubJwtFactoryOptions
                {
                    AppIntegrationId = Options.GitHubAppId,
                    ExpirationSeconds = 600,
                });
            return generator.CreateEncodedJwtToken();
        }

        public async Task<string> GetTokenForInstallation(long installationId)
        {
            var jwt = GetAppToken();
            var product = new ProductHeaderValue(Options.ApplicationName, Options.ApplicationVersion);
            var appClient = new GitHubClient(product)
            {
                Credentials = new Credentials(jwt, AuthenticationType.Bearer)
            };
            var token = await appClient.GitHubApps.CreateInstallationToken(installationId);
            return token.Token;
        }
    }
}