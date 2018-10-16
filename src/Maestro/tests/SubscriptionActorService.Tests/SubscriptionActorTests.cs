// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Autofac;
using Autofac.Extensions.DependencyInjection;
using FluentAssertions;
using Maestro.Contracts;
using Maestro.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Actors;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Build = Maestro.Data.Models.Build;
using SubscriptionPolicy = Maestro.Data.Models.SubscriptionPolicy;
using UpdateFrequency = Maestro.Data.Models.UpdateFrequency;

namespace SubscriptionActorService.Tests
{
    public class SubscriptionActorTests : SubscriptionOrPullRequestActorTests
    {
        private readonly Dictionary<ActorId, Mock<IPullRequestActor>> PullRequestActors =
            new Dictionary<ActorId, Mock<IPullRequestActor>>();

        public SubscriptionActorTests()
        {
            Builder.RegisterInstance((Func<ActorId, IPullRequestActor>)(actorId =>
           {
               Mock<IPullRequestActor> mock = PullRequestActors.GetOrAddValue(actorId, CreateMock<IPullRequestActor>);
               return mock.Object;
           }));
        }

        internal async Task WhenUpdateAsyncIsCalled(Subscription forSubscription, Build andForBuild)
        {
            await Execute(
                async context =>
                {
                    var provider = new AutofacServiceProvider(context);
                    var actorId = new ActorId(forSubscription.Id);
                    var actor = ActivatorUtilities.CreateInstance<SubscriptionActor>(provider, actorId);
                    await actor.UpdateAsync(andForBuild.Id);
                });
        }

        private void ThenUpdateAssetsAsyncShouldHaveBeenCalled(ActorId forActor, Build withBuild)
        {
            var updatedAssets = new List<List<Maestro.Contracts.Asset>>();
            PullRequestActors.Should().ContainKey(forActor)
                .WhichValue
                .Verify(a => a.UpdateAssetsAsync(Subscription.Id, withBuild.Id, NewCommit, Capture.In(updatedAssets)));
            updatedAssets.Should()
                .BeEquivalentTo(
                    new List<List<Maestro.Contracts.Asset>>
                    {
                        withBuild.Assets.Select(
                                a => new Maestro.Contracts.Asset {Name = a.Name, Version = a.Version,})
                            .ToList(),
                    });
        }

        [Fact]
        public async Task BatchableEveryBuildSubscription()
        {
            GivenATestChannel();
            GivenASubscription(
                new SubscriptionPolicy
                {
                    Batchable = true,
                    UpdateFrequency = UpdateFrequency.EveryBuild,
                });
            var b = GivenANewBuild();

            await WhenUpdateAsyncIsCalled(forSubscription: Subscription, andForBuild: b);
            ThenUpdateAssetsAsyncShouldHaveBeenCalled(
                forActor: PullRequestActorId.Create(Subscription.TargetRepository, Subscription.TargetBranch),
                withBuild: b);
        }

        [Fact]
        public async Task NotBatchableEveryBuildSubscription()
        {
            GivenATestChannel();
            GivenASubscription(
                new SubscriptionPolicy
                {
                    Batchable = false,
                    UpdateFrequency = UpdateFrequency.EveryBuild,
                });
            var b = GivenANewBuild();

            await WhenUpdateAsyncIsCalled(forSubscription: Subscription, andForBuild: b);
            ThenUpdateAssetsAsyncShouldHaveBeenCalled(
                forActor: new ActorId(Subscription.Id),
                withBuild: b);
        }
    }
}
