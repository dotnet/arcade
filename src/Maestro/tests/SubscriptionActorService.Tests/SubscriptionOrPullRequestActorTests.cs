// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Maestro.Data;
using Maestro.Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace SubscriptionActorService.Tests
{
    public class SubscriptionOrPullRequestActorTests : ActorTests
    {
        protected const string AssetFeedUrl = "https://source.feed/index.json";
        protected const string SourceBranch = "source.branch";
        protected const string SourceRepo = "source.repo";
        protected const string TargetRepo = "target.repo";
        protected const string TargetBranch = "target.branch";
        protected const string NewBuildNumber = "build.number";
        protected const string NewCommit = "sha2";

        protected readonly Mock<IActionRunner> ActionRunner;

        protected readonly List<Action<BuildAssetRegistryContext>> ContextUpdates =
            new List<Action<BuildAssetRegistryContext>>();

        protected readonly Mock<IHostingEnvironment> HostingEnvironment;

        protected Channel Channel;

        protected Subscription Subscription;

        public SubscriptionOrPullRequestActorTests()
        {
            HostingEnvironment = CreateMock<IHostingEnvironment>();
            Builder.RegisterInstance(HostingEnvironment.Object);

            ActionRunner = CreateMock<IActionRunner>();
            Builder.RegisterInstance(ActionRunner.Object);

            var services = new ServiceCollection();
            services.AddDbContext<BuildAssetRegistryContext>(
                options => { options.UseInMemoryDatabase("BuildAssetRegistry"); });
            Builder.Populate(services);
        }

        protected override async Task BeforeExecute(IComponentContext context)
        {
            var dbContext = context.Resolve<BuildAssetRegistryContext>();
            foreach (Action<BuildAssetRegistryContext> update in ContextUpdates)
            {
                update(dbContext);
            }

            await dbContext.SaveChangesAsync();
        }

        internal void GivenATestChannel()
        {
            Channel = new Channel
            {
                Name = "channel",
                Classification = "class"
            };
            ContextUpdates.Add(context => context.Channels.Add(Channel));
        }

        internal void GivenASubscription(SubscriptionPolicy policy)
        {
            Subscription = new Subscription
            {
                Channel = Channel,
                SourceRepository = SourceRepo,
                TargetRepository = TargetRepo,
                TargetBranch = TargetBranch,
                PolicyObject = policy
            };
            ContextUpdates.Add(context => context.Subscriptions.Add(Subscription));
        }

        internal Build GivenANewBuild((string name, string version)[] assets = null)
        {
            assets = assets ?? new[] {("quail.eating.ducks", "1.1.0"), ("quite.expensive.device", "2.0.1")};
            var build = new Build
            {
                Branch = SourceBranch,
                Repository = SourceRepo,
                BuildNumber = NewBuildNumber,
                Commit = NewCommit,
                DateProduced = DateTimeOffset.UtcNow,
                Assets = new List<Asset>(
                    assets.Select(
                        a => new Asset
                        {
                            Name = a.name,
                            Version = a.version,
                            Locations = new List<AssetLocation>
                            {
                                new AssetLocation
                                {
                                    Location = AssetFeedUrl,
                                    Type = LocationType.NugetFeed
                                }
                            }
                        }))
            };
            ContextUpdates.Add(
                context =>
                {
                    context.Builds.Add(build);
                    context.BuildChannels.Add(
                        new BuildChannel
                        {
                            Build = build,
                            Channel = Channel
                        });
                });
            return build;
        }
    }
}
