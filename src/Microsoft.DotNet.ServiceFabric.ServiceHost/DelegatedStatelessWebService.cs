using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using Microsoft.ServiceFabric.Services.Runtime;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public class DelegatedStatelessWebService<TStartup> : StatelessService
        where TStartup : class
    {
        private readonly Action<IServiceCollection> _configureServices;
        private readonly Action<ContainerBuilder> _configureContainer;

        public DelegatedStatelessWebService(StatelessServiceContext context, Action<IServiceCollection> configureServices, Action<ContainerBuilder> configureContainer)
            : base(context)
        {
            _configureServices = configureServices;
            _configureContainer = configureContainer;
        }

        protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
        {
            return new[]
            {
                new ServiceInstanceListener(
                    context =>
                    {
                        return new HttpSysCommunicationListener(
                            context,
                            "ServiceEndpoint",
                            (url, listener) => new WebHostBuilder()
                                .UseHttpSys()
                                .UseContentRoot(Directory.GetCurrentDirectory())
                                .ConfigureServices(
                                    services =>
                                    {
                                        services.AddAutofac(_configureContainer);
                                        _configureServices(services);
                                        services.AddSingleton<ServiceContext>(context);
                                        services.AddSingleton(context);
                                    })
                                .UseStartup<TStartup>()
                                .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
                                .UseUrls(url)
                                .Build());
                    }),
            };
        }
    }
}
