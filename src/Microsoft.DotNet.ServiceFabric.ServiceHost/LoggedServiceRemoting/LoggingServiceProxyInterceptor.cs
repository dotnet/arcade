// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Fabric;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public class LoggingServiceProxyInterceptor : AsyncInterceptor
    {
        public LoggingServiceProxyInterceptor(
            TelemetryClient telemetryClient,
            ServiceContext context,
            string serviceUri)
        {
            TelemetryClient = telemetryClient;
            Context = context;
            ServiceUri = serviceUri;
        }

        private TelemetryClient TelemetryClient { get; }
        private ServiceContext Context { get; }
        private string ServiceUri { get; }

        protected override async Task InterceptAsync(IInvocation invocation, Func<Task> call)
        {
            string methodName = $"/{invocation.Method?.DeclaringType?.Name}/{invocation.Method?.Name}";
            using (IOperationHolder<DependencyTelemetry> op =
                TelemetryClient.StartOperation<DependencyTelemetry>($"RPC {ServiceUri}{methodName}"))
            {
                try
                {
                    Activity.Current.AddBaggage("CallingServiceName", Context.ServiceName.ToString());
                    op.Telemetry.Type = "ServiceFabricRemoting";
                    op.Telemetry.Target = ServiceUri;
                    op.Telemetry.Data = ServiceUri + methodName;
                    await call();
                }
                catch (Exception ex)
                {
                    op.Telemetry.Success = false;
                    if (ex is AggregateException ae && ae.InnerExceptions.Count == 1)
                    {
                        ex = ae.InnerException;
                    }

                    TelemetryClient.TrackException(ex);
                    ExceptionDispatchInfo.Capture(ex).Throw();
                }
            }
        }

        protected override async Task<T> InterceptAsync<T>(IInvocation invocation, Func<Task<T>> call)
        {
            string methodName = $"/{invocation.Method?.DeclaringType?.Name}/{invocation.Method?.Name}";
            using (IOperationHolder<DependencyTelemetry> op =
                TelemetryClient.StartOperation<DependencyTelemetry>($"RPC {ServiceUri}{methodName}"))
            {
                try
                {
                    Activity.Current.AddBaggage("CallingServiceName", Context.ServiceName.ToString());
                    op.Telemetry.Type = "ServiceFabricRemoting";
                    op.Telemetry.Target = ServiceUri;
                    op.Telemetry.Data = ServiceUri + methodName;
                    return await call();
                }
                catch (Exception ex)
                {
                    op.Telemetry.Success = false;
                    if (ex is AggregateException ae && ae.InnerExceptions.Count == 1)
                    {
                        ex = ae;
                    }

                    TelemetryClient.TrackException(ex);
                    ExceptionDispatchInfo.Capture(ex).Throw();
                    // throw; is Required by the compiler because it doesn't know that ExceptionDispatchInfo.Throw throws
                    // ReSharper disable once HeuristicUnreachableCode
                    throw;
                }
            }
        }

        protected override T Intercept<T>(IInvocation invocation, Func<T> call)
        {
            string methodName = $"/{invocation.Method?.DeclaringType?.Name}/{invocation.Method?.Name}";
            using (IOperationHolder<DependencyTelemetry> op =
                TelemetryClient.StartOperation<DependencyTelemetry>($"RPC {ServiceUri}{methodName}"))
            {
                try
                {
                    Activity.Current.AddBaggage("CallingServiceName", Context.ServiceName.ToString());
                    op.Telemetry.Type = "ServiceFabricRemoting";
                    op.Telemetry.Target = ServiceUri;
                    op.Telemetry.Data = ServiceUri + methodName;
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
