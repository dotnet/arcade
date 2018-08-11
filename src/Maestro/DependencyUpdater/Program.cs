using System.Collections.Generic;
using System.Reflection;
using Autofac;
using Maestro.Contracts;
using Maestro.Data;
using Maestro.GitHub;
using Microsoft.AspNetCore.Hosting;
using Microsoft.DotNet.ServiceFabric.ServiceHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DependencyUpdater
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static void Main()
        {
            ServiceHost.Run(host =>
            {
                host.RegisterStatefulService<DependencyUpdater>("DependencyUpdaterType");
                host.ConfigureContainer(
                    builder =>
                    {
                        builder.AddServiceFabricActor<ISubscriptionActor>();
                    });
                host.ConfigureServices(
                    services =>
                    {
                        services.AddSingleton<IDarcRemoteFactory, DarcRemoteFactory>();
                        services.AddGitHubTokenProvider();
                        services.AddSingleton(provider => ServiceHostConfiguration.Get(provider.GetRequiredService<IHostingEnvironment>()));
                        services.AddDbContext<BuildAssetRegistryContext>(
                            (provider, options) =>
                            {
                                var config = provider.GetRequiredService<IConfigurationRoot>();
                                options.UseSqlServer(config.GetSection("BuildAssetRegistry")["ConnectionString"]);
                            });
                        services.Configure<GitHubTokenProviderOptions>(
                            (options, provider) =>
                            {
                                var config = provider.GetRequiredService<IConfigurationRoot>();
                                var section = config.GetSection("GitHub");
                                section.Bind(options);
                                options.ApplicationName = "Maestro";
                                options.ApplicationVersion = Assembly.GetEntryAssembly()
                                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                    ?.InformationalVersion;
                            });
                    });
            });
        }
    }
}
