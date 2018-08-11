// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Castle.DynamicProxy;
using Castle.DynamicProxy.Generators;
using Castle.DynamicProxy.Internal;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost.Actors
{
    public interface IReminderManager
    {
        Task<IActorReminder> TryRegisterReminderAsync(
            string reminderName,
            byte[] state,
            TimeSpan dueTime,
            TimeSpan period);
        Task TryUnregisterReminderAsync(string reminderName);
    }

    public class DelegatedActor : Actor, IReminderManager, IRemindable
    {
        public DelegatedActor(ActorService actorService, ActorId actorId) : base(actorService, actorId)
        {
        }

        public Task<IActorReminder> TryRegisterReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            try
            {
                return Task.FromResult(GetReminder(reminderName));
            }
            catch (ReminderNotFoundException)
            {
                return RegisterReminderAsync(reminderName, state, dueTime, period);
            }
        }

        public async Task TryUnregisterReminderAsync(string reminderName)
        {
            try
            {
                var reminder = GetReminder(reminderName);
                await UnregisterReminderAsync(reminder);
            }
            catch (ReminderNotFoundException) { }
        }

        public static ChangeNameProxyGenerator Generator { get; } = new ChangeNameProxyGenerator();

        public static (Type, Func<ActorService, ActorId, ILifetimeScope, Action<ContainerBuilder>, ActorBase>) CreateActorTypeAndFactory(string actorName, Type actorType)
        {
            var type = Generator.CreateClassProxyType(
                actorName,
                typeof(DelegatedActor),
                actorType.GetAllInterfaces().Where(i => i.IsAssignableTo<IActor>() || i == typeof(IRemindable)).ToArray(),
                ProxyGenerationOptions.Default);

            ActorBase Factory(ActorService service, ActorId id, ILifetimeScope outerScope, Action<ContainerBuilder> configureScope)
            {
                var args = new object[] {service, id};
                return (ActorBase) Generator.CreateProxyFromProxyType(
                    type,
                    ProxyGenerationOptions.Default,
                    args,
                    new ActorMethodInterceptor(outerScope, actorType));
            }

            return (type, Factory);
        }

        public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            throw new InvalidOperationException("This method call should always be intercepted.");
        }
    }

    internal class ActorMethodInterceptor : AsyncInterceptor
    {
        private readonly ILifetimeScope _outerScope;
        private readonly Type _implementationType;

        public ActorMethodInterceptor(
            ILifetimeScope outerScope,
            Type implementationType)
        {
            _outerScope = outerScope;
            _implementationType = implementationType;
        }

        protected override void Proceed(IInvocation invocation)
        {
            var method = invocation.Method;
            invocation.ReturnValue = method.Invoke(invocation.ReturnValue, invocation.Arguments);
        }

        private void ConfigureScope(Actor actor, ContainerBuilder builder)
        {
            builder.RegisterInstance(actor.StateManager)
                .As<IActorStateManager>();
            builder.RegisterInstance(actor.Id)
                .As<ActorId>();
            builder.RegisterInstance(actor)
                .As<IReminderManager>();
        }

        private bool ShouldIntercept(IInvocation invocation)
        {
            return (invocation.Method.DeclaringType?.IsInterface ?? false) && invocation.Method.DeclaringType != typeof(IReminderManager);
        }

        public override void Intercept(IInvocation invocation)
        {
            if (!ShouldIntercept(invocation))
            {
                invocation.Proceed();
                return;
            }
            base.Intercept(invocation);
        }

        protected override async Task InterceptAsync(IInvocation invocation, Func<Task> call)
        {
            var actor = (Actor) invocation.Proxy;
            using (var scope = _outerScope.BeginLifetimeScope(builder => ConfigureScope(actor, builder)))
            {
                var client = scope.Resolve<TelemetryClient>();
                var context = scope.Resolve<ServiceContext>();
                var id = actor.Id;
                var url = $"{context.ServiceName}/{id}/{invocation.Method?.DeclaringType?.Name}/{invocation.Method?.Name}";
                using (var op = client.StartOperation<RequestTelemetry>($"RPC {url}"))
                {
                    try
                    {
                        op.Telemetry.Url = new Uri(url);

                        invocation.ReturnValue = scope.Resolve(_implementationType);
                        await call();
                    }
                    catch (Exception ex)
                    {
                        op.Telemetry.Success = false;
                        client.TrackException(ex);
                        throw;
                    }
                }
            }
        }

        protected override async Task<T> InterceptAsync<T>(IInvocation invocation, Func<Task<T>> call)
        {
            var actor = (Actor) invocation.Proxy;
            using (var scope = _outerScope.BeginLifetimeScope(builder => ConfigureScope(actor, builder)))
            {
                var client = scope.Resolve<TelemetryClient>();
                var context = scope.Resolve<ServiceContext>();
                var id = actor.Id;
                var url = $"{context.ServiceName}/{id}/{invocation.Method?.DeclaringType?.Name}/{invocation.Method?.Name}";
                using (var op = client.StartOperation<RequestTelemetry>($"RPC {url}"))
                {
                    try
                    {
                        op.Telemetry.Url = new Uri(url);

                        invocation.ReturnValue = scope.Resolve(_implementationType);
                        return await call();
                    }
                    catch (Exception ex)
                    {
                        op.Telemetry.Success = false;
                        client.TrackException(ex);
                        throw;
                    }
                }
            }
        }

        protected override T Intercept<T>(IInvocation invocation, Func<T> call)
        {
            var actor = (Actor) invocation.Proxy;
            using (var scope = _outerScope.BeginLifetimeScope(builder => ConfigureScope(actor, builder)))
            {
                var client = scope.Resolve<TelemetryClient>();
                var context = scope.Resolve<ServiceContext>();
                var id = actor.Id;
                var url = $"{context.ServiceName}/{id}/{invocation.Method?.DeclaringType?.Name}/{invocation.Method?.Name}";
                using (var op = client.StartOperation<RequestTelemetry>($"RPC {url}"))
                {
                    try
                    {
                        op.Telemetry.Url = new Uri(url);

                        invocation.ReturnValue = scope.Resolve(_implementationType);
                        return call();
                    }
                    catch (Exception ex)
                    {
                        op.Telemetry.Success = false;
                        client.TrackException(ex);
                        throw;
                    }
                }
            }
        }
    }

    public class ChangeNameProxyGenerator : ProxyGenerator
    {
        public ChangeNameProxyGenerator()
            :base(new CustomProxyBuilder())
        {
        }

        private class CustomProxyBuilder : DefaultProxyBuilder
        {
            public CustomProxyBuilder(): base(new CustomModuleScope()) { }
        }

        private class CustomModuleScope : ModuleScope
        {
            public CustomModuleScope(): base(false, false, new CustomNamingScope(), DEFAULT_ASSEMBLY_NAME, DEFAULT_FILE_NAME, DEFAULT_ASSEMBLY_NAME, DEFAULT_FILE_NAME) { }
        }

        private class CustomNamingScope : NamingScope, INamingScope
        {
            public static volatile string SuggestedName;
            public static volatile string CurrentName;

            string INamingScope.GetUniqueName(string suggestedName)
            {
                if (suggestedName == SuggestedName)
                {
                    return CurrentName;
                }

                return base.GetUniqueName(suggestedName);
            }
        }

        public Type CreateClassProxyType(
            string name,
            Type classToProxy,
            Type[] additionalInterfacesToProxy,
            ProxyGenerationOptions options)
        {
            CustomNamingScope.SuggestedName = "Castle.Proxies." + classToProxy.Name + "Proxy";
            CustomNamingScope.CurrentName = name;
            return CreateClassProxyType(classToProxy, additionalInterfacesToProxy, options);
        }

        public object CreateProxyFromProxyType(Type proxyType, ProxyGenerationOptions options, object[] constructorArguments, params IInterceptor[] interceptors)
        {
            List<object> proxyArguments = BuildArgumentListForClassProxy(options, interceptors);
            if (constructorArguments != null && constructorArguments.Length != 0)
              proxyArguments.AddRange(constructorArguments);
            return CreateClassProxyInstance(proxyType, proxyArguments, proxyType, constructorArguments);
        }
    }

    public class DelegatedActorService<TServiceImplementation, TActorImplementation> : ActorService
        where TServiceImplementation : IServiceImplementation
    {
        private readonly Action<IServiceCollection> _configureServices;
        private readonly Action<ContainerBuilder> _configureContainer;
        private readonly Func<ActorService, ActorId, ILifetimeScope, Action<ContainerBuilder>, ActorBase> _actorFactory;
        private ILifetimeScope _container;

        public DelegatedActorService(
            StatefulServiceContext context,
            ActorTypeInformation actorTypeInfo,
            Action<IServiceCollection> configureServices,
            Action<ContainerBuilder> configureContainer,
            Func<ActorService, ActorId, ILifetimeScope, Action<ContainerBuilder>, ActorBase> actorFactory,
            ActorServiceSettings settings = null) : base(context, actorTypeInfo, ActorFactory, null, new KvsActorStateProvider(), settings)
        {
            _configureServices = configureServices;
            _configureContainer = configureContainer;
            _actorFactory = actorFactory;
        }

        protected override async Task OnOpenAsync(ReplicaOpenMode openMode, CancellationToken cancellationToken)
        {
            await base.OnOpenAsync(openMode, cancellationToken);

            var services = new ServiceCollection();
            services.AddSingleton<ServiceContext>(Context);
            services.AddSingleton(Context);
            _configureServices(services);
            var builder = new ContainerBuilder();
            builder.Populate(services);
            _configureContainer(builder);
            _container = builder.Build();
        }

        protected override async Task OnCloseAsync(CancellationToken cancellationToken)
        {
            await base.OnCloseAsync(cancellationToken);
            _container?.Dispose();
        }

        protected override void OnAbort()
        {
            base.OnAbort();
            _container?.Dispose();
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            var baseListeners = base.CreateServiceReplicaListeners();
            var ifaces = typeof(TServiceImplementation).GetAllInterfaces()
                .Where(iface => iface.IsAssignableTo<IService>())
                .ToArray();
            if (ifaces.Length == 0)
            {
                return baseListeners;
            }

            return baseListeners.Concat(new[]
            {
                new ServiceReplicaListener(
                    context => ServiceHostRemoting.CreateServiceRemotingListener<TServiceImplementation>(
                        context,
                        ifaces,
                        _container)),
            });
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            await base.RunAsync(cancellationToken);

            using (ILifetimeScope scope = _container.BeginLifetimeScope())
            {
                var impl = scope.Resolve<TServiceImplementation>();
                var telemetryClient = scope.Resolve<TelemetryClient>();
                var logger = scope.Resolve<ILogger<DelegatedActorService<TServiceImplementation, TActorImplementation>>>();

                await Task.WhenAll(
                    impl.RunAsync(cancellationToken),
                    ScheduledService.RunScheduleAsync(impl, telemetryClient, cancellationToken, logger));
            }
        }

        private ActorBase CreateActor(ActorId actorId)
        {
            return _actorFactory(this, actorId, _container, builder => { });
        }

        private static ActorBase ActorFactory(ActorService service, ActorId actorId)
        {
            return ((DelegatedActorService<TServiceImplementation, TActorImplementation>) service).CreateActor(actorId);
        }
    }
}
