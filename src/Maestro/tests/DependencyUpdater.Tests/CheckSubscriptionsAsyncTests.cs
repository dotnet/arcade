using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Maestro.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Helix.ServiceHost;
using Microsoft.ServiceFabric.Data;
using Moq;
using Xunit;

namespace DependencyUpdater.Tests
{
    public class CheckSubscriptionsAsyncTests : DependencyUpdaterTests
    {
        [Fact]
        public async Task NeedsUpdateSubscription()
        {
            var channel = new Channel {Name = "channel", Classification = "class"};
            var oldBuild = new Build
            {
                Branch = "source.branch",
                Repository = "source.repo",
                BuildNumber = "old.build.number",
                Commit = "oldSha",
                DateProduced = DateTimeOffset.UtcNow.AddDays(-2)
            };
            var location = "https://source.feed/index.json";
            var build = new Build
            {
                Branch = "source.branch",
                Repository = "source.repo",
                BuildNumber = "build.number",
                Commit = "sha",
                DateProduced = DateTimeOffset.UtcNow,
                Assets = new List<Asset>
                {
                    new Asset
                    {
                        Name = "source.asset",
                        Version = "1.0.1",
                        Locations = new List<AssetLocation>
                        {
                            new AssetLocation {Location = location, Type = LocationType.NugetFeed}
                        }
                    }
                }
            };
            var buildChannel = new BuildChannel {Build = build, Channel = channel};
            var subscription = new Subscription
            {
                Channel = channel,
                SourceRepository = "source.repo",
                TargetRepository = "target.repo",
                TargetBranch = "target.branch",
                PolicyUpdateFrequency = UpdateFrequency.EveryDay,
                Policy = new SubscriptionPolicy
                {
                    MergePolicy = MergePolicy.Never,
                    UpdateFrequency = UpdateFrequency.EveryDay
                },
                LastAppliedBuild = oldBuild
            };
            await Context.Subscriptions.AddAsync(subscription);
            await Context.BuildChannels.AddAsync(buildChannel);
            var pr = "https://pr.url/1";
            await Context.SaveChangesAsync();
            Darc.Setup(d => d.CreatePrAsync("target.repo", "target.branch", It.IsAny<IList<DarcAsset>>()))
                .ReturnsAsync(
                    (string repo, string branch, IList<DarcAsset> assets) =>
                    {
                        assets.Should().BeEquivalentTo(new DarcAsset("source.asset", "1.0.1", "sha", location));
                        return pr;
                    });

            var updater = ActivatorUtilities.CreateInstance<DependencyUpdater>(Scope.ServiceProvider);
            await updater.CheckSubscriptionsAsync(CancellationToken.None);
            using (ITransaction tx = StateManager.CreateTransaction())
            {
                (await PullRequests.ToListAsync(tx)).Should()
                    .BeEquivalentTo(
                        KeyValuePair.Create(
                            pr,
                            new InProgressPullRequest
                            {
                                BuildId = build.Id,
                                SubscriptionId = subscription.Id,
                                Url = pr
                            }));
                (await PullRequestsBySubscription.ToListAsync(tx)).Should()
                    .BeEquivalentTo(KeyValuePair.Create(subscription.Id, pr));
                Assert.Equal(1, await PullRequestsBySubscription.GetCountAsync(tx));
            }
        }

        [Fact]
        public async Task NeedsUpdateWithExistingPrSubscription()
        {
            var channel = new Channel {Name = "channel", Classification = "class"};
            var oldBuild = new Build
            {
                Branch = "source.branch",
                Repository = "source.repo",
                BuildNumber = "old.build.number",
                Commit = "oldSha",
                DateProduced = DateTimeOffset.UtcNow.AddDays(-2)
            };
            var location = "https://source.feed/index.json";
            var build = new Build
            {
                Branch = "source.branch",
                Repository = "source.repo",
                BuildNumber = "build.number",
                Commit = "sha",
                DateProduced = DateTimeOffset.UtcNow,
                Assets = new List<Asset>
                {
                    new Asset
                    {
                        Name = "source.asset",
                        Version = "1.0.1",
                        Locations = new List<AssetLocation>
                        {
                            new AssetLocation {Location = location, Type = LocationType.NugetFeed}
                        }
                    }
                }
            };
            var buildChannel = new BuildChannel {Build = build, Channel = channel};
            var subscription = new Subscription
            {
                Channel = channel,
                SourceRepository = "source.repo",
                TargetRepository = "target.repo",
                TargetBranch = "target.branch",
                PolicyUpdateFrequency = UpdateFrequency.EveryDay,
                Policy = new SubscriptionPolicy
                {
                    MergePolicy = MergePolicy.Never,
                    UpdateFrequency = UpdateFrequency.EveryDay
                },
                LastAppliedBuild = oldBuild
            };
            await Context.Subscriptions.AddAsync(subscription);
            await Context.BuildChannels.AddAsync(buildChannel);
            var pr = "https://pr.url/1";
            await Context.SaveChangesAsync();
            using (ITransaction tx = StateManager.CreateTransaction())
            {
                await PullRequests.AddAsync(
                    tx,
                    pr,
                    new InProgressPullRequest {BuildId = oldBuild.Id, SubscriptionId = subscription.Id, Url = pr});
                await PullRequestsBySubscription.AddAsync(tx, subscription.Id, pr);
                await tx.CommitAsync();
            }

            Darc.Setup(d => d.UpdatePrAsync(pr, "target.repo", "target.branch", It.IsAny<IList<DarcAsset>>()))
                .Callback(
                    (string _, string __, string ___, IList<DarcAsset> assets) =>
                    {
                        assets.Should().BeEquivalentTo(new DarcAsset("source.asset", "1.0.1", "sha", location));
                    })
                .Returns(Task.CompletedTask);

            var updater = ActivatorUtilities.CreateInstance<DependencyUpdater>(Scope.ServiceProvider);
            await updater.CheckSubscriptionsAsync(CancellationToken.None);
            using (ITransaction tx = StateManager.CreateTransaction())
            {
                (await PullRequests.ToListAsync(tx)).Should()
                    .BeEquivalentTo(
                        KeyValuePair.Create(
                            pr,
                            new InProgressPullRequest
                            {
                                BuildId = build.Id,
                                SubscriptionId = subscription.Id,
                                Url = pr
                            }));
                (await PullRequestsBySubscription.ToListAsync(tx)).Should()
                    .BeEquivalentTo(KeyValuePair.Create(subscription.Id, pr));
                Assert.Equal(1, await PullRequestsBySubscription.GetCountAsync(tx));
            }
        }

        [Fact]
        public async Task OneEveryBuildSubscription()
        {
            var channel = new Channel {Name = "channel", Classification = "class"};
            var subscription = new Subscription
            {
                Channel = channel,
                SourceRepository = "source.repo",
                TargetRepository = "target.repo",
                TargetBranch = "target.branch",
                PolicyUpdateFrequency = UpdateFrequency.EveryDay,
                Policy = new SubscriptionPolicy
                {
                    MergePolicy = MergePolicy.Never,
                    UpdateFrequency = UpdateFrequency.EveryBuild
                }
            };
            await Context.Subscriptions.AddAsync(subscription);
            await Context.SaveChangesAsync();

            var updater = ActivatorUtilities.CreateInstance<DependencyUpdater>(Scope.ServiceProvider);
            await updater.CheckSubscriptionsAsync(CancellationToken.None);
            using (ITransaction tx = StateManager.CreateTransaction())
            {
                Assert.Equal(0, await PullRequests.GetCountAsync(tx));
                Assert.Equal(0, await PullRequestsBySubscription.GetCountAsync(tx));
            }
        }

        [Fact]
        public async Task UpToDateSubscription()
        {
            var channel = new Channel {Name = "channel", Classification = "class"};
            var build = new Build
            {
                Branch = "source.branch",
                Repository = "source.repo",
                BuildNumber = "build.number",
                Commit = "sha",
                DateProduced = DateTimeOffset.UtcNow
            };
            var buildChannel = new BuildChannel {Build = build, Channel = channel};
            var subscription = new Subscription
            {
                Channel = channel,
                SourceRepository = "source.repo",
                TargetRepository = "target.repo",
                TargetBranch = "target.branch",
                PolicyUpdateFrequency = UpdateFrequency.EveryDay,
                Policy = new SubscriptionPolicy
                {
                    MergePolicy = MergePolicy.Never,
                    UpdateFrequency = UpdateFrequency.EveryDay
                },
                LastAppliedBuild = build
            };
            await Context.Subscriptions.AddAsync(subscription);
            await Context.BuildChannels.AddAsync(buildChannel);
            await Context.SaveChangesAsync();

            var updater = ActivatorUtilities.CreateInstance<DependencyUpdater>(Scope.ServiceProvider);
            await updater.CheckSubscriptionsAsync(CancellationToken.None);
            using (ITransaction tx = StateManager.CreateTransaction())
            {
                Assert.Equal(0, await PullRequests.GetCountAsync(tx));
                Assert.Equal(0, await PullRequestsBySubscription.GetCountAsync(tx));
            }
        }
    }
}
