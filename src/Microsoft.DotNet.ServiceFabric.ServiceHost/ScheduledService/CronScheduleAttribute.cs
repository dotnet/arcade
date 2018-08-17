// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using JetBrains.Annotations;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    [AttributeUsage(AttributeTargets.Method)]
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
