// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Health;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
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
            CodePackageActivationContext packageActivationContext = FabricRuntime.GetActivationContext();
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                ServicePointManager.CheckCertificateRevocationList = true;
                JsonConvert.DefaultSettings =
                    () => new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.None};
                var host = new ServiceHost();
                configure(host);
                host.Start();
                packageActivationContext.ReportDeployedServicePackageHealth(
                    new HealthInformation("ServiceHost", "ServiceHost.Run", HealthState.Ok));
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                packageActivationContext.ReportDeployedServicePackageHealth(
                    new HealthInformation("ServiceHost", "ServiceHost.Run", HealthState.Error)
                    {
                        Description = $"Unhandled Exception: {ex}",
                    },
                    new HealthReportSendOptions {Immediate = true});
                Thread.Sleep(5000);
                Environment.Exit(-1);
            }
        }

        private ServiceHost()
        {
        }

        private readonly List<Func<Task>> _serviceCallbacks = new List<Func<Task>>();

        private readonly List<Action<ContainerBuilder>> _configureContainerActions =
            new List<Action<ContainerBuilder>> {ConfigureDefaultContainer};

        private readonly List<Action<IServiceCollection>> _configureServicesActions =
            new List<Action<IServiceCollection>> {ConfigureDefaultServices};

        public ServiceHost ConfigureServices(Action<IServiceCollection> configure)
        {
            _configureServicesActions.Add(configure);
            return this;
        }

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
            _serviceCallbacks.Add(() => ServiceRuntime.RegisterServiceAsync(serviceTypeName, ctor));
        }

        private void RegisterStatefulService<TService>(
            string serviceTypeName,
            Func<StatefulServiceContext, TService> ctor)
            where TService : StatefulService
        {
            _serviceCallbacks.Add(() => ServiceRuntime.RegisterServiceAsync(serviceTypeName, ctor));
        }

        public ServiceHost RegisterStatefulService<TService>(string serviceTypeName)
            where TService : IServiceImplementation
        {
            RegisterStatefulService(
                serviceTypeName,
                context => new DelegatedStatefulService<TService>(
                    context,
                    ApplyConfigurationToServices,
                    ApplyConfigurationToContainer));
            return ConfigureContainer(
                builder =>
                {
                    builder.RegisterType<TService>().As<TService>().InstancePerDependency();
                });
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

        private void Start()
        {
            foreach (Func<Task> svc in _serviceCallbacks)
            {
                svc().GetAwaiter().GetResult();
            }
        }

        private static void ConfigureDefaultContainer(ContainerBuilder builder)
        {
        }

        private static void ConfigureDefaultServices(IServiceCollection services)
        {
            services.AddOptions();
            services.SetupConfiguration();
            services.TryAddSingleton(InitializeEnvironment());
            ConfigureApplicationInsights(services);
            services.AddLogging(
                builder =>
                {
                    builder.AddDebug();
                    builder.AddFixedApplicationInsights(LogLevel.Information);
                });
        }

        private static IHostingEnvironment InitializeEnvironment()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables(prefix: "ASPNETCORE_")
                .Build();
            var options = new WebHostOptions(config, GetApplicationName());
            var env = new HostingEnvironment();
            env.Initialize(AppContext.BaseDirectory, options);
            return env;
        }

        private static string GetApplicationName()
        {
            return Environment.GetEnvironmentVariable("Fabric_ApplicationName");
        }
    }

    public static class ServiceHostConfiguration
    {
        public static KeyVaultClient GetKeyVaultClient(string connectionString)
        {
            var provider = new AzureServiceTokenProvider(connectionString);
            return new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(provider.KeyVaultTokenCallback));
        }

        public static bool RunningInServiceFabric()
        {
            string fabricApplication = Environment.GetEnvironmentVariable("Fabric_ApplicationName");
            return !string.IsNullOrEmpty(fabricApplication);
        }

        public static IConfigurationRoot Get(IHostingEnvironment env)
        {
            IConfigurationRoot bootstrapConfig = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile(".config/settings.json")
                .AddJsonFile($".config/settings.{env.EnvironmentName}.json")
                .Build();

            Func<KeyVaultClient> clientFactory;
            if (env.IsDevelopment() && RunningInServiceFabric())
            {
                var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";
                string appId = "388be541-91ed-4771-8473-5791e071ed14";
                string certThumbprint = "C4DFDCC47D95C1C64B55B42946CCEFDDF9E46FAB";

                string connectionString = $"RunAs=App;AppId={appId};TenantId={tenantId};CertificateThumbprint={certThumbprint};CertificateStoreLocation=LocalMachine";
                clientFactory = () => GetKeyVaultClient(connectionString);
            }
            else
            {
                clientFactory = () => GetKeyVaultClient(null);
            }

            string keyVaultUri = bootstrapConfig["KeyVaultUri"];


            return new ConfigurationBuilder().SetBasePath(env.ContentRootPath)
                .AddKeyVaultMappedJsonFile(".config/settings.json", keyVaultUri, clientFactory)
                .AddKeyVaultMappedJsonFile($".config/settings.{env.EnvironmentName}.json", keyVaultUri, clientFactory)
                .Build();
        }
    }
}
