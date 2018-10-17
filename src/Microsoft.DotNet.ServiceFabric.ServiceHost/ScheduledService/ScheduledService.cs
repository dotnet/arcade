// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Simpl;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    internal static class ScheduledService
    {
        private static IEnumerable<(IJobDetail job, ITrigger trigger)> GetCronJobs(
            object service,
            TelemetryClient telemetryClient,
            ILogger logger)
        {
            Type type = service.GetType();

            foreach (MethodInfo method in type.GetRuntimeMethods())
            {
                if (method.IsStatic)
                {
                    continue;
                }

                if (method.GetParameters().Length > 1)
                {
                    continue;
                }

                if (method.ReturnType != typeof(Task))
                {
                    continue;
                }

                var attr = method.GetCustomAttribute<CronScheduleAttribute>();
                if (attr == null)
                {
                    continue;
                }

                IJobDetail job = JobBuilder.Create<MethodInvokingJob>()
                    .WithIdentity(method.Name, type.Name)
                    .UsingJobData(
                        new JobDataMap
                        {
                            ["service"] = service,
                            ["method"] = method,
                            ["telemetryClient"] = telemetryClient
                        })
                    .Build();

                TimeZoneInfo scheduleTimeZone = TimeZoneInfo.Utc;

                try
                {
                    scheduleTimeZone = TimeZoneInfo.FindSystemTimeZoneById(attr.TimeZone);
                }
                catch (TimeZoneNotFoundException)
                {
                    logger.LogWarning(
                        "TimeZoneNotFoundException occurred for timezone string: {requestedTimeZoneName}",
                        attr.TimeZone);
                }
                catch (InvalidTimeZoneException)
                {
                    logger.LogWarning(
                        "InvalidTimeZoneException occurred for timezone string: {requestedTimeZoneName}",
                        attr.TimeZone);
                }

                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity(method.Name + "-Trigger", type.Name)
                    .WithCronSchedule(attr.Schedule, schedule => schedule.InTimeZone(scheduleTimeZone))
                    .StartNow()
                    .Build();

                yield return (job, trigger);
            }
        }

        private static IEnumerable<(IJobDetail job, ITrigger trigger)> GetRunnableJobs(
            IScheduledService svc,
            TelemetryClient telemetryClient)
        {
            foreach ((Func<Task> func, string schedule) in svc.GetScheduledJobs())
            {
                string id = Guid.NewGuid().ToString();
                IJobDetail job = JobBuilder.Create<FuncInvokingJob>()
                    .WithIdentity(id)
                    .UsingJobData(
                        new JobDataMap
                        {
                            ["func"] = func,
                            ["telemetryClient"] = telemetryClient
                        })
                    .Build();

                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity(id + "-Trigger")
                    .WithCronSchedule(schedule)
                    .StartNow()
                    .Build();

                yield return (job, trigger);
            }
        }

        public static async Task RunScheduleAsync(
            object service,
            TelemetryClient telemetryClient,
            CancellationToken cancellationToken,
            ILogger logger)
        {
            IScheduler scheduler = null;
            string name = Guid.NewGuid().ToString();
            try
            {
                DirectSchedulerFactory.Instance.CreateScheduler(name, name, new DefaultThreadPool(), new RAMJobStore());
                scheduler = await DirectSchedulerFactory.Instance.GetScheduler(name, cancellationToken);
                foreach ((IJobDetail job, ITrigger trigger) in GetCronJobs(service, telemetryClient, logger))
                {
                    await scheduler.ScheduleJob(job, trigger, cancellationToken);
                }

                if (service is IScheduledService svc)
                {
                    foreach ((IJobDetail job, ITrigger trigger) in GetRunnableJobs(svc, telemetryClient))
                    {
                        await scheduler.ScheduleJob(job, trigger, cancellationToken);
                    }
                }

                await scheduler.Start(cancellationToken);
                await cancellationToken.AsTask();
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("OperationCanceledException while processing schedule job.");
            }

            if (scheduler != null)
            {
                await scheduler.Shutdown(true, CancellationToken.None);
            }
        }
    }
}
