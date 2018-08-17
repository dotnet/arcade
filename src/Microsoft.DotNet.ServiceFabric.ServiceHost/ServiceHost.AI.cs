// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Fabric;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector;
using Microsoft.ApplicationInsights.ServiceFabric;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    partial class ServiceHost
    {
        private static string GetApplicationInsightsKey()
        {
            string envVar = Environment.GetEnvironmentVariable("APPLICATION_INSIGHTS_KEY");
            if (string.IsNullOrEmpty(envVar))
            {
                // ReSharper disable once ImpureMethodCallOnReadonlyValueField
                return Guid.Empty.ToString("D");
            }

            return envVar;
        }

        private static FabricTelemetryInitializer ConfigureFabricTelemetryInitializer(IServiceProvider provider)
        {
            var props = new Dictionary<string, string>();
            var serviceContext = provider.GetService<ServiceContext>();
            if (serviceContext != null)
            {
                props["ServiceFabric.ServiceName"] = serviceContext.ServiceName.ToString();
                props["cloud_RoleName"] = serviceContext.ServiceName.ToString();
                props["ServiceFabric.ServiceTypeName"] = serviceContext.ServiceTypeName;
                props["ServiceFabric.PartitionId"] = serviceContext.PartitionId.ToString();
                props["ServiceFabric.ApplicationName"] = serviceContext.CodePackageActivationContext.ApplicationName;
                props["ServiceFabric.ApplicationTypeName"] =
                    serviceContext.CodePackageActivationContext.ApplicationTypeName;
                props["ServiceFabric.NodeName"] = serviceContext.NodeContext.NodeName;
                if (serviceContext is StatelessServiceContext)
                {
                    props["ServiceFabric.InstanceId"] =
                        serviceContext.ReplicaOrInstanceId.ToString(CultureInfo.InvariantCulture);
                }

                if (serviceContext is StatefulServiceContext)
                {
                    props["ServiceFabric.ReplicaId"] =
                        serviceContext.ReplicaOrInstanceId.ToString(CultureInfo.InvariantCulture);
                }
            }

            return new FabricTelemetryInitializer(props);
        }

        internal static void RemoveImplementations(IServiceCollection services, params Type[] types)
        {
            foreach (ServiceDescriptor badRegistration in services
                .Where(desc => types.Contains(desc.ImplementationType))
                .ToList())
            {
                services.Remove(badRegistration);
            }
        }

        private static void ConfigureApplicationInsights(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry();
            RemoveImplementations(services, typeof(PerformanceCollectorModule));
            services.AddSingleton<ITelemetryModule>(
                provider =>
                {
                    var collector = new PerformanceCollectorModule();
                    collector.DefaultCounters.Add(
                        new PerformanceCounterCollectionRequest(
                            "\\Process(??APP_WIN32_PROC??)\\% Processor Time",
                            "\\Process(??APP_WIN32_PROC??)\\% Processor Time"));
                    collector.DefaultCounters.Add(
                        new PerformanceCounterCollectionRequest(
                            "\\Process(??APP_WIN32_PROC??)\\% Processor Time Normalized",
                            "\\Process(??APP_WIN32_PROC??)\\% Processor Time Normalized"));
                    collector.DefaultCounters.Add(
                        new PerformanceCounterCollectionRequest(
                            "\\Memory\\Available Bytes",
                            "\\Memory\\Available Bytes"));
                    collector.DefaultCounters.Add(
                        new PerformanceCounterCollectionRequest(
                            "\\Process(??APP_WIN32_PROC??)\\Private Bytes",
                            "\\Process(??APP_WIN32_PROC??)\\Private Bytes"));
                    collector.DefaultCounters.Add(
                        new PerformanceCounterCollectionRequest(
                            "\\Process(??APP_WIN32_PROC??)\\IO Data Bytes/sec",
                            "\\Process(??APP_WIN32_PROC??)\\IO Data Bytes/sec"));
                    collector.DefaultCounters.Add(
                        new PerformanceCounterCollectionRequest(
                            "\\Processor(_Total)\\% Processor Time",
                            "\\Processor(_Total)\\% Processor Time"));
                    return collector;
                });

            services.AddSingleton<ITelemetryInitializer>(ConfigureFabricTelemetryInitializer);
            services.AddSingleton<ITelemetryInitializer>(new RichExceptionTelemetryInitializer());
            services.Configure<ApplicationInsightsServiceOptions>(ConfigureApplicationInsightsOptions);
        }

        private static void ConfigureApplicationInsightsOptions(ApplicationInsightsServiceOptions options)
        {
            options.ApplicationVersion = Assembly.GetEntryAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion;
            options.InstrumentationKey = GetApplicationInsightsKey();
            options.EnableQuickPulseMetricStream = false;
            options.EnableAdaptiveSampling = false;
        }
    }
}
