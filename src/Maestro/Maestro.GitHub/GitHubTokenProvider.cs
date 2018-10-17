// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using GitHubJwt;
using Microsoft.Extensions.Options;
using Octokit;

namespace Maestro.GitHub
{
    public class GitHubTokenProvider : IGitHubTokenProvider
    {
        private readonly IOptions<GitHubTokenProviderOptions> _options;

        public GitHubTokenProvider(IOptions<GitHubTokenProviderOptions> options)
        {
            _options = options;
        }

        public GitHubTokenProviderOptions Options => _options.Value;

        public async Task<string> GetTokenForInstallation(long installationId)
        {
            string jwt = GetAppToken();
            var product = new ProductHeaderValue(Options.ApplicationName, Options.ApplicationVersion);
            var appClient = new GitHubClient(product) {Credentials = new Credentials(jwt, AuthenticationType.Bearer)};
            AccessToken token = await appClient.GitHubApps.CreateInstallationToken(installationId);
            return token.Token;
        }

        private string GetAppToken()
        {
            var generator = new GitHubJwtFactory(
                new StringPrivateKeySource(Options.PrivateKey),
                new GitHubJwtFactoryOptions
                {
                    AppIntegrationId = Options.GitHubAppId,
                    ExpirationSeconds = 600
                });
            return generator.CreateEncodedJwtToken();
        }
    }
}
