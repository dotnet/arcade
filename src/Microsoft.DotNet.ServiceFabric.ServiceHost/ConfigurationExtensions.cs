using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public static class ConfigurationExtensions
    {
        [PublicAPI]
        public static IServiceCollection SetupConfiguration(this IServiceCollection services)
        {
            services.TryAddSingleton<IServiceContext, ServiceFabricServiceContext>();
            services.TryAddSingleton<IServiceConfig>(provider =>
            {
                var context = provider.GetRequiredService<IServiceContext>();
                return new EnvironmentConfigMapper(context.Config);
            });
            return services;
        }
    }
}
