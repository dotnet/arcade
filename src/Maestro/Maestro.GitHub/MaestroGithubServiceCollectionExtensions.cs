using System;
using Microsoft.Extensions.DependencyInjection;

namespace Maestro.GitHub
{
    public static class MaestroGithubServiceCollectionExtensions
    {
        public static IServiceCollection AddGitHubTokenProvider(this IServiceCollection services)
        {
            return services.AddSingleton<IGitHubTokenProvider, GitHubTokenProvider>();
        }
    }
}
