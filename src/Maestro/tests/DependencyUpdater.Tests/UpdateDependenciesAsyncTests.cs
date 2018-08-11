using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Maestro.Data.Models;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Helix.ServiceHost;
using Microsoft.ServiceFabric.Data;
using Moq;
using Xunit;
using Channel = Maestro.Data.Models.Channel;
using MergePolicy = Maestro.Data.Models.MergePolicy;
using SubscriptionPolicy = Maestro.Data.Models.SubscriptionPolicy;
using UpdateFrequency = Maestro.Data.Models.UpdateFrequency;

namespace DependencyUpdater.Tests
{
    public class UpdateDependenciesAsyncTests : DependencyUpdaterTests
    {
        [Fact]
        public async Task EveryBuildSubscription()
        {
            var channel = new Channel {Name = "channel", Classification = "class"};
            var build = new Build
            {
                Branch = "source.branch",
                Repository = "source.repo",
                BuildNumber = "build.number",
                Commit = "sha",
                DateProduced = DateTimeOffset.UtcNow.AddDays(-1)
            };
            var location = "https://repo.feed/index.json";
            var newAsset = new Asset
            {
                Name = "source.asset",
                Version = "1.0.1",
                Locations = new List<AssetLocation>
                {
                    new AssetLocation {Location = location, Type = LocationType.NugetFeed}
                }
            };
            var newBuild = new Build
            {
                Branch = "source.branch",
                Repository = "source.repo",
                BuildNumber = "build.number.2",
                Commit = "sha2",
                DateProduced = DateTimeOffset.UtcNow,
                Assets = new List<Asset> {newAsset}
            };
            var buildChannels = new[]
            {
                new BuildChannel {Build = build, Channel = channel},
                new BuildChannel {Build = newBuild, Channel = channel}
            };
            var subscription = new Subscription
            {
                Channel = channel,
                SourceRepository = "source.repo",
                TargetRepository = "target.repo",
                TargetBranch = "target.branch",
                PolicyObject = new SubscriptionPolicy
                {
                    MergePolicy = MergePolicy.Never,
                    UpdateFrequency = UpdateFrequency.EveryBuild
                },
                LastAppliedBuild = build
            };
            var repoInstallation = new RepoInstallation
            {
                Repository = "target.repo",
                InstallationId = 1,
            };
            await Context.BuildChannels.AddRangeAsync(buildChannels);
            await Context.Subscriptions.AddAsync(subscription);
            await Context.RepoInstallations.AddAsync(repoInstallation);
            await Context.SaveChangesAsync();

            var pr = "http://repo.pr/1";
            Darc.Setup(
                    d => d.CreatePullRequestAsync(
                        subscription.TargetRepository,
                        subscription.TargetBranch,
                        newBuild.Commit,
                        It.IsAny<IList<AssetData>>(),
                        null,
                        null,
                        null))
                .ReturnsAsync(
                    (string repo, string branch, string commit, IList<AssetData> assets, string baseBranch, string title, string description) =>
                    {
                        assets.Should()
                            .BeEquivalentTo(new AssetData {Name = newAsset.Name, Version = newAsset.Version});
                        return pr;
                    });

            var updater = ActivatorUtilities.CreateInstance<DependencyUpdater>(Scope.ServiceProvider);
            await updater.UpdateDependenciesAsync(newBuild.Id, channel.Id);
            using (ITransaction tx = StateManager.CreateTransaction())
            {
                (await PullRequests.ToListAsync(tx)).Should()
                    .BeEquivalentTo(
                        KeyValuePair.Create(
                            pr,
                            new InProgressPullRequest
                            {
                                BuildId = newBuild.Id,
                                SubscriptionId = subscription.Id,
                                Url = pr
                            }));
                (await PullRequestsBySubscription.ToListAsync(tx)).Should()
                    .BeEquivalentTo(KeyValuePair.Create(subscription.Id, pr));
            }
        }

        [Fact]
        public async Task NoSubscriptions()
        {
            var channel = new Channel {Name = "channel", Classification = "class"};
            var build = new Build
            {
                Branch = "source.branch",
                Repository = "source.repo",
                BuildNumber = "build.number",
                Commit = "sha",
                DateProduced = DateTimeOffset.UtcNow.AddDays(-1)
            };
            var newBuild = new Build
            {
                Branch = "source.branch",
                Repository = "source.repo",
                BuildNumber = "build.number.2",
                Commit = "sha2",
                DateProduced = DateTimeOffset.UtcNow
            };
            var buildChannels = new[]
            {
                new BuildChannel {Build = build, Channel = channel},
                new BuildChannel {Build = newBuild, Channel = channel}
            };
            await Context.BuildChannels.AddRangeAsync(buildChannels);
            await Context.SaveChangesAsync();

            var updater = ActivatorUtilities.CreateInstance<DependencyUpdater>(Scope.ServiceProvider);
            await updater.UpdateDependenciesAsync(newBuild.Id, channel.Id);
            using (ITransaction tx = StateManager.CreateTransaction())
            {
                Assert.Equal(0, await PullRequests.GetCountAsync(tx));
                Assert.Equal(0, await PullRequestsBySubscription.GetCountAsync(tx));
            }
        }
    }
}
