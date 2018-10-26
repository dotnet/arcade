// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using FluentAssertions;
using Maestro.Contracts;
using Maestro.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Actors;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using Xunit;
using Asset = Maestro.Contracts.Asset;

namespace SubscriptionActorService.Tests
{
    public class SubscriptionActorTests : SubscriptionOrPullRequestActorTests
    {
        public SubscriptionActorTests()
        {
            Builder.RegisterInstance(
                (Func<ActorId, IPullRequestActor>) (actorId =>
                {
                    Mock<IPullRequestActor> mock = PullRequestActors.GetOrAddValue(
                        actorId,
                        CreateMock<IPullRequestActor>);
                    return mock.Object;
                }));
        }

        private readonly Dictionary<ActorId, Mock<IPullRequestActor>> PullRequestActors =
            new Dictionary<ActorId, Mock<IPullRequestActor>>();

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
            var updatedAssets = new List<List<Asset>>();
            PullRequestActors.Should()
                .ContainKey(forActor)
                .WhichValue.Verify(
                    a => a.UpdateAssetsAsync(Subscription.Id, withBuild.Id, NewCommit, Capture.In(updatedAssets)));
            updatedAssets.Should()
                .BeEquivalentTo(
                    new List<List<Asset>>
                    {
                        withBuild.Assets.Select(
                                a => new Asset
                                {
                                    Name = a.Name,
                                    Version = a.Version
                                })
                            .ToList()
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
                    UpdateFrequency = UpdateFrequency.EveryBuild
                });
            Build b = GivenANewBuild();

            await WhenUpdateAsyncIsCalled(Subscription, b);
            ThenUpdateAssetsAsyncShouldHaveBeenCalled(
                PullRequestActorId.Create(Subscription.TargetRepository, Subscription.TargetBranch),
                b);
        }

        [Fact]
        public async Task NotBatchableEveryBuildSubscription()
        {
            GivenATestChannel();
            GivenASubscription(
                new SubscriptionPolicy
                {
                    Batchable = false,
                    UpdateFrequency = UpdateFrequency.EveryBuild
                });
            Build b = GivenANewBuild();

            await WhenUpdateAsyncIsCalled(Subscription, b);
            ThenUpdateAssetsAsyncShouldHaveBeenCalled(new ActorId(Subscription.Id), b);
        }
    }
}
