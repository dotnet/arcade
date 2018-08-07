using System;
using System.Fabric;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public class LoggingServiceInterceptor : AsyncInterceptor
    {
        private ServiceContext Context { get; }
        private TelemetryClient TelemetryClient { get; }

        public LoggingServiceInterceptor(ServiceContext context, TelemetryClient telemetryClient)
        {
            Context = context;
            TelemetryClient = telemetryClient;
        }

        protected override async Task InterceptAsync(IInvocation invocation, Func<Task> call)
        {
            var url = $"{Context.ServiceName}/{invocation.Method?.DeclaringType?.Name}/{invocation.Method?.Name}";
            using (var op = TelemetryClient.StartOperation<RequestTelemetry>($"RPC {url}"))
            {
                try
                {
                    op.Telemetry.Url = new Uri(url);
                    await call();
                }
                catch (Exception ex)
                {
                    op.Telemetry.Success = false;
                    TelemetryClient.TrackException(ex);
                    throw;
                }
            }
        }

        protected override async Task<T> InterceptAsync<T>(IInvocation invocation, Func<Task<T>> call)
        {
            var url = $"{Context.ServiceName}/{invocation.Method?.DeclaringType?.Name}/{invocation.Method?.Name}";
            using (var op = TelemetryClient.StartOperation<RequestTelemetry>($"RPC {url}"))
            {
                try
                {
                    op.Telemetry.Url = new Uri(url);
                    return await call();
                }
                catch (Exception ex)
                {
                    op.Telemetry.Success = false;
                    TelemetryClient.TrackException(ex);
                    throw;
                }
            }
        }

        protected override T Intercept<T>(IInvocation invocation, Func<T> call)
        {
            var url = $"{Context.ServiceName}/{invocation.Method?.DeclaringType?.Name}/{invocation.Method?.Name}";
            using (var op = TelemetryClient.StartOperation<RequestTelemetry>($"RPC {url}"))
            {
                try
                {
                    op.Telemetry.Url = new Uri(url);
                    return call();
                }
                catch (Exception ex)
                {
                    op.Telemetry.Success = false;
                    TelemetryClient.TrackException(ex);
                    throw;
                }
            }
        }
    }
}