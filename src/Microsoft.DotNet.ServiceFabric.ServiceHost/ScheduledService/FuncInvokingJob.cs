// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            var func = (Func<Task>) context.MergedJobDataMap["func"];
            var telemetryClient = (TelemetryClient) context.MergedJobDataMap["telemetryClient"];
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
