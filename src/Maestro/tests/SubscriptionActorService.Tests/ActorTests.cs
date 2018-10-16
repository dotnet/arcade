using System.Collections.Generic;
using Autofac;
using FluentAssertions;
using Microsoft.DotNet.ServiceFabric.ServiceHost.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using ServiceFabricMocks;

namespace SubscriptionActorService.Tests
{
    public class ActorTests : TestsWithServices
    {
        protected readonly MockActorStateManager StateManager;
        protected readonly MockReminderManager Reminders;

        protected readonly Dictionary<string, MockReminderManager.Reminder> ExpectedReminders =
            new Dictionary<string, MockReminderManager.Reminder>();

        protected readonly Dictionary<string, object> ExpectedActorState = new Dictionary<string, object>();

        public ActorTests()
        {
            StateManager = new MockActorStateManager();
            Reminders = new MockReminderManager();

            Builder.RegisterInstance(StateManager)
                .As<IActorStateManager>();
            Builder.RegisterInstance(Reminders)
                .As<IReminderManager>();
        }

        public override void Dispose()
        {
            Reminders.Data.Should().BeEquivalentTo(ExpectedReminders);
            StateManager.Data.Should().BeEquivalentTo(ExpectedActorState);
            base.Dispose();
        }
    }
}