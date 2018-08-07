using System;
using System.Fabric;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Castle.DynamicProxy;
using Castle.DynamicProxy.Internal;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ServiceFabric.Actors.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public static class ServiceHostRemoting
    {
        internal static IServiceRemotingListener CreateServiceRemotingListener<TImplementation>(
            StatefulServiceContext context,
            Type[] ifaces,
            ILifetimeScope container)
        {
            var client = container.Resolve<TelemetryClient>();
            Type firstIface = ifaces[0];
            Type[] additionalIfaces = ifaces.Skip(1).ToArray();
            var gen = new ProxyGenerator();
            var impl = (IService) gen.CreateInterfaceProxyWithoutTarget(
                firstIface,
                additionalIfaces,
                new InvokeInNewScopeInterceptor<TImplementation>(container),
                new LoggingServiceInterceptor(context, client));

            return new FabricTransportServiceRemotingListener(
                context,
                new ActivityServiceRemotingMessageDispatcher(context, impl, null));
        }
    }

    public static class ServiceHostProxy
    {
        private static ServiceProxyFactory CreateFactory()
        {
            return new ServiceProxyFactory(
                handler => new ActivityServiceRemotingClientFactory(
                    new FabricTransportActorRemotingClientFactory(handler)));
        }

        public static T Create<T>(
            Uri serviceUri,
            TelemetryClient telemetryClient,
            ServiceContext context,
            ServicePartitionKey partitionKey = null,
            TargetReplicaSelector targetReplicaSelector = TargetReplicaSelector.Default)
            where T : class, IService
        {
            var service = CreateFactory().CreateServiceProxy<T>(serviceUri, partitionKey, targetReplicaSelector);
            var gen = new ProxyGenerator();
            T proxy = gen.CreateInterfaceProxyWithTargetInterface(service,
                new LoggingServiceProxyInterceptor(telemetryClient, context, serviceUri.ToString()));
            return proxy;
        }
    }

    internal class InvokeInNewScopeInterceptor<TImplementation> : AsyncInterceptor
    {
        private readonly ILifetimeScope _outerScope;

        public InvokeInNewScopeInterceptor(ILifetimeScope outerScope)
        {
            _outerScope = outerScope;
        }

        protected override async Task InterceptAsync(IInvocation invocation, Func<Task> call)
        {
            using (var scope = _outerScope.BeginLifetimeScope())
            {
                var client = scope.Resolve<TelemetryClient>();
                var context = scope.Resolve<ServiceContext>();
                var url = $"{context.ServiceName}/{invocation.Method?.DeclaringType?.Name}/{invocation.Method?.Name}";
                using (var op = client.StartOperation<RequestTelemetry>($"RPC {url}"))
                {
                    try
                    {
                        op.Telemetry.Url = new Uri(url);

                        var instance = scope.Resolve<TImplementation>();
                        ((IChangeProxyTarget) invocation).ChangeInvocationTarget(instance);
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
            using (var scope = _outerScope.BeginLifetimeScope())
            {
                var client = scope.Resolve<TelemetryClient>();
                var context = scope.Resolve<ServiceContext>();
                var url = $"{context.ServiceName}/{invocation.Method?.DeclaringType?.Name}/{invocation.Method?.Name}";
                using (var op = client.StartOperation<RequestTelemetry>($"RPC {url}"))
                {
                    try
                    {
                        op.Telemetry.Url = new Uri(url);

                        var instance = scope.Resolve<TImplementation>();
                        ((IChangeProxyTarget) invocation).ChangeInvocationTarget(instance);
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
            using (var scope = _outerScope.BeginLifetimeScope())
            {
                var client = scope.Resolve<TelemetryClient>();
                var context = scope.Resolve<ServiceContext>();
                var url = $"{context.ServiceName}/{invocation.Method?.DeclaringType?.Name}/{invocation.Method?.Name}";
                using (var op = client.StartOperation<RequestTelemetry>($"RPC {url}"))
                {
                    try
                    {
                        op.Telemetry.Url = new Uri(url);

                        var instance = scope.Resolve<TImplementation>();
                        ((IChangeProxyTarget) invocation).ChangeInvocationTarget(instance);
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
}

