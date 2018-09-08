using Microsoft.Extensions.DependencyInjection;

namespace Maestro.MergePolicies
{
    public static class MergePolicyServiceCollectionExtensions
    {
        public static IServiceCollection AddMergePolicies(this IServiceCollection services)
        {
            services.AddTransient<MergePolicy, NoExtraCommitsMergePolicy>();
            services.AddTransient<MergePolicy, RequireSuccessfulChecksMergePolicy>();
            services.AddTransient<MergePolicy, AllChecksSuccessfulMergePolicy>();
            return services;
        }
    }
}
