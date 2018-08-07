using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    internal static class ScheduledService
    {
        private static IEnumerable<(IJobDetail job, ITrigger trigger)> GetCronJobs(object service, TelemetryClient telemetryClient, ILogger logger)
        {
            var type = service.GetType();

            foreach (var method in type.GetRuntimeMethods())
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
                var job = JobBuilder.Create<MethodInvokingJob>()
                    .WithIdentity(method.Name, type.Name)
                    .UsingJobData(new JobDataMap
                    {
                        ["service"] = service,
                        ["method"] = method,
                        ["telemetryClient"] = telemetryClient
                    })
                    .Build();

                var scheduleTimeZone = TimeZoneInfo.Utc;

                try
                {
                    scheduleTimeZone = TimeZoneInfo.FindSystemTimeZoneById(attr.TimeZone);
                }
                catch (TimeZoneNotFoundException)
                {
                    logger.LogWarning("TimeZoneNotFoundException occurred for timezone string: {requestedTimeZoneName}", attr.TimeZone);
                }
                catch (InvalidTimeZoneException)
                {
                    logger.LogWarning("InvalidTimeZoneException occurred for timezone string: {requestedTimeZoneName}", attr.TimeZone);
                }

                var trigger = TriggerBuilder.Create()
                    .WithIdentity(method.Name + "-Trigger", type.Name)
                    .WithCronSchedule(
                        attr.Schedule,
                        schedule => schedule.InTimeZone(scheduleTimeZone))
                    .StartNow()
                    .Build();

                yield return (job, trigger);
            }
        }

        private static IEnumerable<(IJobDetail job, ITrigger trigger)> GetRunnableJobs(IScheduledService svc, TelemetryClient telemetryClient)
        {
            foreach (var (func, schedule) in svc.GetScheduledJobs())
            {
                var id = Guid.NewGuid().ToString();
                var job = JobBuilder.Create<FuncInvokingJob>()
                    .WithIdentity(id)
                    .UsingJobData(new JobDataMap
                    {
                        ["func"] = func,
                        ["telemetryClient"] = telemetryClient
                    })
                    .Build();

                var trigger = TriggerBuilder.Create()
                    .WithIdentity(id + "-Trigger")
                    .WithCronSchedule(schedule)
                    .StartNow()
                    .Build();

                yield return (job, trigger);
            }
        }

        public static async Task RunScheduleAsync(object service, TelemetryClient telemetryClient, CancellationToken cancellationToken, ILogger logger)
        {
            IScheduler scheduler = null;
            try
            {
                Quartz.Impl.DirectSchedulerFactory.Instance.CreateVolatileScheduler(maxThreads: 2);
                scheduler = await Quartz.Impl.DirectSchedulerFactory.Instance.GetScheduler(cancellationToken);
                foreach (var (job, trigger) in GetCronJobs(service, telemetryClient, logger))
                {
                    await scheduler.ScheduleJob(job, trigger, cancellationToken);
                }
                if (service is IScheduledService svc)
                {
                    foreach (var (job, trigger) in GetRunnableJobs(svc, telemetryClient))
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
