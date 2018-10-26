// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Fabric;
using System.Fabric.Health;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.DotNet.ServiceFabric.ServiceHost.Actors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    /// <summary>
    ///     A Service Fabric service host that supports activating services via dependency injection.
    /// </summary>
    public partial class ServiceHost
    {
        private readonly List<Action<ContainerBuilder>> _configureContainerActions =
            new List<Action<ContainerBuilder>> {ConfigureDefaultContainer};

        private readonly List<Action<IServiceCollection>> _configureServicesActions =
            new List<Action<IServiceCollection>> {ConfigureDefaultServices};

        private readonly List<Func<Task>> _serviceCallbacks = new List<Func<Task>>();

        private ServiceHost()
        {
        }

        /// <summary>
        ///     Configure and run a new ServiceHost
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
                        Description = $"Unhandled Exception: {ex}"
                    },
                    new HealthReportSendOptions {Immediate = true});
                Thread.Sleep(5000);
                Environment.Exit(-1);
            }
        }

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
            Func<StatelessServiceContext, TService> ctor) where TService : StatelessService
        {
            _serviceCallbacks.Add(() => ServiceRuntime.RegisterServiceAsync(serviceTypeName, ctor));
        }

        private void RegisterStatefulService<TService>(
            string serviceTypeName,
            Func<StatefulServiceContext, TService> ctor) where TService : StatefulService
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
                builder => { builder.RegisterType<TService>().As<TService>().InstancePerDependency(); });
        }

        private void RegisterActorService<TService, TActor>(
            Func<StatefulServiceContext, ActorTypeInformation, TService> ctor)
            where TService : ActorService where TActor : Actor
        {
            _serviceCallbacks.Add(() => ActorRuntime.RegisterActorAsync<TActor>(ctor));
        }

        private void RegisterStatefulActorService<TActor>(
            string actorName,
            Func<StatefulServiceContext, ActorTypeInformation,
                Func<ActorService, ActorId, ILifetimeScope, Action<ContainerBuilder>, ActorBase>, ActorService> ctor)
            where TActor : IActor
        {
            (Type actorType,
                    Func<ActorService, ActorId, ILifetimeScope, Action<ContainerBuilder>, ActorBase> actorFactory) =
                DelegatedActor.CreateActorTypeAndFactory(actorName, typeof(TActor));
            // ReSharper disable once PossibleNullReferenceException
            // The method search parameters are hard coded
            MethodInfo registerActorAsyncMethod = typeof(ActorRuntime).GetMethod(
                    "RegisterActorAsync",
                    new[]
                    {
                        typeof(Func<StatefulServiceContext, ActorTypeInformation, ActorService>),
                        typeof(TimeSpan),
                        typeof(CancellationToken)
                    })
                .MakeGenericMethod(actorType);
            _serviceCallbacks.Add(
                () => (Task) registerActorAsyncMethod.Invoke(
                    null,
                    new object[]
                    {
                        (Func<StatefulServiceContext, ActorTypeInformation, ActorService>) ((context, info) =>
                            ctor(context, info, actorFactory)),
                        default(TimeSpan),
                        default(CancellationToken)
                    }));
        }

        public ServiceHost RegisterStatefulActorService<
            [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
            TActor>(string actorName) where TActor : IActor
        {
            RegisterStatefulActorService<TActor>(
                actorName,
                (context, info, actorFactory) =>
                {
                    return new DelegatedActorService<TActor>(
                        context,
                        info,
                        ApplyConfigurationToServices,
                        ApplyConfigurationToContainer,
                        actorFactory);
                });
            return ConfigureContainer(
                builder => { builder.RegisterType<TActor>().As<TActor>().InstancePerDependency(); });
        }

        public ServiceHost RegisterStatefulActorService<
            [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
            TService, [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
            TActor>(string actorName) where TService : IServiceImplementation where TActor : IActor
        {
            RegisterStatefulActorService<TActor>(
                actorName,
                (context, info, actorFactory) =>
                {
                    return new DelegatedActorService<TService, TActor>(
                        context,
                        info,
                        ApplyConfigurationToServices,
                        ApplyConfigurationToContainer,
                        actorFactory);
                });
            return ConfigureContainer(
                builder =>
                {
                    builder.RegisterType<TActor>().As<TActor>().InstancePerDependency();
                    builder.RegisterType<TService>().As<TService>().InstancePerDependency();
                });
        }

        public ServiceHost RegisterStatelessWebService<TStartup>(string serviceTypeName) where TStartup : class
        {
            RegisterStatelessService(
                serviceTypeName,
                context => new DelegatedStatelessWebService<TStartup>(
                    context,
                    ApplyConfigurationToServices,
                    ApplyConfigurationToContainer));
            return ConfigureContainer(
                builder => { builder.RegisterType<TStartup>().As<TStartup>().InstancePerDependency(); });
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
            IConfigurationRoot config = new ConfigurationBuilder().AddEnvironmentVariables("ASPNETCORE_").Build();
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
        private static string GetAzureServiceTokenProviderConnectionString(IHostingEnvironment env)
        {
            if (env.IsDevelopment() && RunningInServiceFabric())
            {
                var tenantId = "72f988bf-86f1-41af-91ab-2d7cd011db47";
                var appId = "388be541-91ed-4771-8473-5791e071ed14";
                var certThumbprint = "C4DFDCC47D95C1C64B55B42946CCEFDDF9E46FAB";

                string connectionString =
                    $"RunAs=App;AppId={appId};TenantId={tenantId};CertificateThumbprint={certThumbprint};CertificateStoreLocation=LocalMachine";
                return connectionString;
            }

            return null;
        }

        public static KeyVaultClient GetKeyVaultClient(IHostingEnvironment env)
        {
            string connectionString = GetAzureServiceTokenProviderConnectionString(env);
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
            IConfigurationRoot bootstrapConfig = new ConfigurationBuilder().SetBasePath(env.ContentRootPath)
                .AddJsonFile(".config/settings.json")
                .AddJsonFile($".config/settings.{env.EnvironmentName}.json")
                .Build();

            Func<KeyVaultClient> clientFactory = () => GetKeyVaultClient(env);
            string keyVaultUri = bootstrapConfig["KeyVaultUri"];

            return new ConfigurationBuilder().SetBasePath(env.ContentRootPath)
                .AddKeyVaultMappedJsonFile(".config/settings.json", keyVaultUri, clientFactory)
                .AddKeyVaultMappedJsonFile($".config/settings.{env.EnvironmentName}.json", keyVaultUri, clientFactory)
                .Build();
        }
    }
}
