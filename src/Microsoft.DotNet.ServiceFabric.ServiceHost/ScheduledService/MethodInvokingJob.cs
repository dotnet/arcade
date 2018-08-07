using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Quartz;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    [DisallowConcurrentExecution]
    internal sealed class MethodInvokingJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var service = context.MergedJobDataMap["service"];
            var method = (MethodInfo)context.MergedJobDataMap["method"];
            var telemetryClient = (TelemetryClient)context.MergedJobDataMap["telemetryClient"];
            try
            {
                telemetryClient.TrackTrace($"Invoking job {method.DeclaringType}.{method.Name}", SeverityLevel.Information);
                var param = method.GetParameters();
                if (param.Length == 1 && param[0].ParameterType == typeof(CancellationToken))
                {
                    await (Task)method.Invoke(service, new object[] { context.CancellationToken });
                }
                else
                {
                    await (Task)method.Invoke(service, Array.Empty<object>());
                }
                telemetryClient.TrackTrace($"Successfully finished job {method.DeclaringType}.{method.Name}", SeverityLevel.Information);
            }
            catch (Exception ex)
            {
                telemetryClient.TrackException(ex, new Dictionary<string, string>
                {
                    ["scheduledMethod"] = method.Name,
                    ["serviceType"] = service?.GetType().FullName
                });
            }
        }
    }
}