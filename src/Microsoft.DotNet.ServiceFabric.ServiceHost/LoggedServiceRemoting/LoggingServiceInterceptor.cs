// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Fabric;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public class LoggingServiceInterceptor : AsyncInterceptor
    {
        public LoggingServiceInterceptor(ServiceContext context, TelemetryClient telemetryClient)
        {
            Context = context;
            TelemetryClient = telemetryClient;
        }

        private ServiceContext Context { get; }
        private TelemetryClient TelemetryClient { get; }

        protected override async Task InterceptAsync(IInvocation invocation, Func<Task> call)
        {
            string url = $"{Context.ServiceName}/{invocation.Method?.DeclaringType?.Name}/{invocation.Method?.Name}";
            using (IOperationHolder<RequestTelemetry> op =
                TelemetryClient.StartOperation<RequestTelemetry>($"RPC {url}"))
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
            string url = $"{Context.ServiceName}/{invocation.Method?.DeclaringType?.Name}/{invocation.Method?.Name}";
            using (IOperationHolder<RequestTelemetry> op =
                TelemetryClient.StartOperation<RequestTelemetry>($"RPC {url}"))
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
            string url = $"{Context.ServiceName}/{invocation.Method?.DeclaringType?.Name}/{invocation.Method?.Name}";
            using (IOperationHolder<RequestTelemetry> op =
                TelemetryClient.StartOperation<RequestTelemetry>($"RPC {url}"))
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
