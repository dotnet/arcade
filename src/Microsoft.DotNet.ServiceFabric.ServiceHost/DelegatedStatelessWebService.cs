// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Fabric;
using System.IO;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Services.Communication.AspNetCore;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public class DelegatedStatelessWebService<TStartup> : StatelessService where TStartup : class
    {
        private readonly Action<ContainerBuilder> _configureContainer;
        private readonly Action<IServiceCollection> _configureServices;

        public DelegatedStatelessWebService(
            StatelessServiceContext context,
            Action<IServiceCollection> configureServices,
            Action<ContainerBuilder> configureContainer) : base(context)
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
                            (url, listener) => new WebHostBuilder().UseHttpSys()
                                .UseContentRoot(Directory.GetCurrentDirectory())
                                .UseSetting(WebHostDefaults.ApplicationKey, typeof(TStartup).Assembly.GetName().Name)
                                .ConfigureServices(
                                    services =>
                                    {
                                        services.AddAutofac(_configureContainer);
                                        services.AddSingleton<ServiceContext>(context);
                                        services.AddSingleton(context);
                                        services.AddSingleton<IStartup>(
                                            provider =>
                                            {
                                                var env = provider.GetRequiredService<IHostingEnvironment>();
                                                return new DelegatedStatelessWebServiceStartup<TStartup>(
                                                    provider,
                                                    env,
                                                    _configureServices);
                                            });
                                    })
                                .UseServiceFabricIntegration(listener, ServiceFabricIntegrationOptions.None)
                                .UseUrls(url)
                                .Build());
                    })
            };
        }
    }

    public class DelegatedStatelessWebServiceStartup<TStartup> : IStartup
    {
        private readonly Action<IServiceCollection> _configureServices;
        private readonly IStartup _startupImplementation;

        public DelegatedStatelessWebServiceStartup(
            IServiceProvider provider,
            IHostingEnvironment env,
            Action<IServiceCollection> configureServices)
        {
            _configureServices = configureServices;
            if (typeof(TStartup).IsAssignableTo<IStartup>())
            {
                _startupImplementation = (IStartup) ActivatorUtilities.CreateInstance<TStartup>(provider);
            }
            else
            {
                StartupMethods methods = StartupLoader.LoadMethods(provider, typeof(TStartup), env.EnvironmentName);
                _startupImplementation = new ConventionBasedStartup(methods);
            }
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            _configureServices(services);
            return _startupImplementation.ConfigureServices(services);
        }

        public void Configure(IApplicationBuilder app)
        {
            _startupImplementation.Configure(app);
        }
    }
}
