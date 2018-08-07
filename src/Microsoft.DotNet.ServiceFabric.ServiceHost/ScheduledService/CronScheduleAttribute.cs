using System;
using JetBrains.Annotations;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [MeansImplicitUse]
    public sealed class CronScheduleAttribute : Attribute
    {
        public CronScheduleAttribute(string schedule, string timezone)
        {
            Schedule = schedule;
            TimeZone = timezone;
        }

        public string Schedule { get; }
        public string TimeZone { get; }
    }
}