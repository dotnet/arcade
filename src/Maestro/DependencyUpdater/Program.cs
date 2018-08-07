using System.Collections.Generic;
using System.Threading.Tasks;
using Maestro.Data;
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
                host.ConfigureServices(
                    services =>
                    {
                        services.AddSingleton<IDarc>(new NullDarc());
                        services.AddSingleton(provider => ServiceHostConfiguration.Get(provider.GetRequiredService<IHostingEnvironment>()));
                        services.AddDbContext<BuildAssetRegistryContext>(
                            (provider, options) =>
                            {
                                var config = provider.GetRequiredService<IConfigurationRoot>();
                                options.UseSqlServer(config.GetSection("BuildAssetRegistry")["ConnectionString"]);
                            });
                    });
            });
        }
    }

    internal class NullDarc : IDarc
    {
        public Task<string> CreatePrAsync(string repository, string branch, IList<DarcAsset> assets)
        {
            throw new System.NotImplementedException();
        }

        public Task UpdatePrAsync(string pullRequest, string repository, string branch, IList<DarcAsset> assets)
        {
            throw new System.NotImplementedException();
        }

        public Task<PrStatus> GetPrStatusAsync(string pullRequest)
        {
            throw new System.NotImplementedException();
        }

        public Task MergePrAsync(string pullRequest)
        {
            throw new System.NotImplementedException();
        }

        public Task<IReadOnlyList<Check>> GetPrChecksAsync(string pullRequest)
        {
            throw new System.NotImplementedException();
        }
    }
}
