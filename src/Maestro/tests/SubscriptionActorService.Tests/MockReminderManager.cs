// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.DotNet.ServiceFabric.ServiceHost.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;

namespace SubscriptionActorService.Tests
{
    public class MockReminderManager : IReminderManager
    {
        public readonly Dictionary<string, Reminder> Data = new Dictionary<string, Reminder>();

        public Task<IActorReminder> TryRegisterReminderAsync(
            string reminderName,
            byte[] state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            if (!Data.TryGetValue(reminderName, out Reminder value))
            {
                value = Data[reminderName] = new Reminder(reminderName, state, dueTime, period);
            }

            return Task.FromResult((IActorReminder) value);
        }

        public Task TryUnregisterReminderAsync(string reminderName)
        {
            Data.Remove(reminderName);
            return Task.CompletedTask;
        }

        public class Reminder : IActorReminder
        {
            public Reminder(string name, byte[] state, TimeSpan dueTime, TimeSpan period)
            {
                Name = name;
                State = state;
                DueTime = dueTime;
                Period = period;
            }

            public string Name { get; }
            public TimeSpan DueTime { get; }
            public TimeSpan Period { get; }
            public byte[] State { get; }
        }
    }
}
