using System;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Quartz;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    [DisallowConcurrentExecution]
    internal sealed class FuncInvokingJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var func = (Func<Task>)context.MergedJobDataMap["func"];
            var telemetryClient = (TelemetryClient)context.MergedJobDataMap["telemetryClient"];
            try
            {
                await func();
            }
            catch (Exception ex)
            {
                telemetryClient.TrackException(ex);
            }
        }
    }
}