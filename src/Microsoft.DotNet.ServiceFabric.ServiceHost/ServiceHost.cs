using System;
using System.Collections.Generic;
using System.Fabric;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Services.Runtime;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    /// <summary>
    /// A Service Fabric service host that supports activating services via dependency injection.
    /// </summary>
    public partial class ServiceHost
    {
        /// <summary>
        /// Configure and run a new ServiceHost
        /// </summary>
        public static void Run(Action<ServiceHost> configure)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            ServicePointManager.CheckCertificateRevocationList = true;
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None
            };
            var host = new ServiceHost();
            configure(host);
            host.Run();
        }

        private ServiceHost()
        {
        }

        private readonly List<Func<Task>> _serviceCallbacks = new List<Func<Task>>();

        private readonly List<Action<ContainerBuilder>> _configureContainerActions =
            new List<Action<ContainerBuilder>> {ConfigureDefaultContainer};

        private readonly List<Action<IServiceCollection>> _configureServicesActions =
            new List<Action<IServiceCollection>> {ConfigureDefaultServices};

        public ServiceHost ConfigureContainer(Action<ContainerBuilder> configure)
        {
            _configureContainerActions.Add(configure);
            return this;
        }

        private void ApplyConfigurationToContainer(ContainerBuilder builder)
        {
            foreach (Action<ContainerBuilder> act in _configureContainerActions)
            {
                act(builder);
            }
        }

        private void ApplyConfigurationToServices(IServiceCollection services)
        {
            foreach (Action<IServiceCollection> act in _configureServicesActions)
            {
                act(services);
            }
        }

        private void RegisterStatelessService<TService>(
            string serviceTypeName,
            Func<StatelessServiceContext, TService> ctor)
            where TService : StatelessService
        {
            _serviceCallbacks.Add(
                () => ServiceRuntime.RegisterServiceAsync(serviceTypeName,
                    ctor));
        }

        public ServiceHost RegisterStatelessWebService<TStartup>(string serviceTypeName)
            where TStartup : class
        {
            RegisterStatelessService(
                serviceTypeName,
                context => new DelegatedStatelessWebService<TStartup>(context, ApplyConfigurationToServices, ApplyConfigurationToContainer));
            return ConfigureContainer(
                builder =>
                {
                    builder.RegisterType<TStartup>().As<TStartup>().InstancePerDependency();
                });
        }

        private void Run()
        {
            foreach (Func<Task> svc in _serviceCallbacks)
            {
                svc().GetAwaiter().GetResult();
            }

            Thread.Sleep(Timeout.Infinite);
        }

        private static void ConfigureDefaultContainer(ContainerBuilder builder)
        {
        }

        private static void ConfigureDefaultServices(IServiceCollection services)
        {
            services.AddOptions();
            services.SetupConfiguration();
            services.TryAddSingleton<IHostingEnvironment, HostingEnvironment>();
            ConfigureApplicationInsights(services);
            services.AddLogging(
                builder =>
                {
                    builder.AddDebug();
                    builder.AddFixedApplicationInsights(LogLevel.Information);
                });
        }
    }
}
