using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Maestro.Data.Models;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Helix.ServiceHost;
using Microsoft.ServiceFabric.Data;
using Moq;
using Xunit;
using Xunit.Sdk;
using Channel = Maestro.Data.Models.Channel;
using MergePolicy = Maestro.Data.Models.MergePolicy;
using SubscriptionPolicy = Maestro.Data.Models.SubscriptionPolicy;
using UpdateFrequency = Maestro.Data.Models.UpdateFrequency;

namespace DependencyUpdater.Tests
{
    public class CheckInProgressPRsAsyncTests : DependencyUpdaterTests
    {
        [Theory]
        [InlineData(PrStatus.Open)]
        [InlineData(PrStatus.Closed)]
        [InlineData(PrStatus.Merged)]
        public async Task MergePolicyNeverManuallyMerged(PrStatus status)
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
                PolicyObject = new SubscriptionPolicy
                {
                    MergePolicy = MergePolicy.Never,
                    UpdateFrequency = UpdateFrequency.EveryBuild
                }
            };
            var repoInstallation = new RepoInstallation
            {
                Repository = "target.repo",
                InstallationId = 1,
            };
            await Context.RepoInstallations.AddAsync(repoInstallation);
            await Context.BuildChannels.AddAsync(buildChannel);
            await Context.Subscriptions.AddAsync(subscription);
            await Context.SaveChangesAsync();

            var pr = "https://pr.url/1";
            using (ITransaction tx = StateManager.CreateTransaction())
            {
                await PullRequests.AddAsync(
                    tx,
                    pr,
                    new InProgressPullRequest {BuildId = build.Id, SubscriptionId = subscription.Id, Url = pr});
                await PullRequestsBySubscription.AddAsync(tx, subscription.Id, pr);
                await tx.CommitAsync();
            }

            Darc.Setup(d => d.GetPullRequestStatusAsync(pr)).ReturnsAsync(status);

            var updater = ActivatorUtilities.CreateInstance<DependencyUpdater>(Scope.ServiceProvider);
            await updater.CheckInProgressPRsAsync(CancellationToken.None);

            switch (status)
            {
                case PrStatus.Open:
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
                    }

                    break;
                case PrStatus.Merged:
                    Assert.Equal(build.Id, Context.Subscriptions.First().LastAppliedBuildId);
                    goto closed;
                case PrStatus.Closed:
                    Assert.Null(Context.Subscriptions.First().LastAppliedBuildId);
                    closed:
                    using (ITransaction tx = StateManager.CreateTransaction())
                    {
                        Assert.Equal(0, await PullRequests.GetCountAsync(tx));
                        Assert.Equal(0, await PullRequestsBySubscription.GetCountAsync(tx));
                    }

                    break;
                default:
                    throw new XunitException($"Unknown status '{status}'");
            }
        }

        [Theory]
        [InlineData(PrStatus.Closed)]
        [InlineData(PrStatus.Merged)]
        public async Task MergePolicyBuildSucceeded(PrStatus status)
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
                PolicyObject = new SubscriptionPolicy
                {
                    MergePolicy = MergePolicy.BuildSucceeded,
                    UpdateFrequency = UpdateFrequency.EveryBuild
                }
            };
            var repoInstallation = new RepoInstallation
            {
                Repository = "target.repo",
                InstallationId = 1,
            };
            await Context.RepoInstallations.AddAsync(repoInstallation);
            await Context.BuildChannels.AddAsync(buildChannel);
            await Context.Subscriptions.AddAsync(subscription);
            await Context.SaveChangesAsync();

            var pr = "https://pr.url/1";
            using (ITransaction tx = StateManager.CreateTransaction())
            {
                await PullRequests.AddAsync(
                    tx,
                    pr,
                    new InProgressPullRequest {BuildId = build.Id, SubscriptionId = subscription.Id, Url = pr});
                await PullRequestsBySubscription.AddAsync(tx, subscription.Id, pr);
                await tx.CommitAsync();
            }

            Darc.Setup(d => d.GetPullRequestStatusAsync(pr)).ReturnsAsync(status);

            var updater = ActivatorUtilities.CreateInstance<DependencyUpdater>(Scope.ServiceProvider);
            await updater.CheckInProgressPRsAsync(CancellationToken.None);

            switch (status)
            {
                case PrStatus.Merged:
                    Assert.Equal(build.Id, Context.Subscriptions.First().LastAppliedBuildId);
                    goto closed;
                case PrStatus.Closed:
                    Assert.Null(Context.Subscriptions.First().LastAppliedBuildId);
                    closed:
                    using (ITransaction tx = StateManager.CreateTransaction())
                    {
                        Assert.Equal(0, await PullRequests.GetCountAsync(tx));
                        Assert.Equal(0, await PullRequestsBySubscription.GetCountAsync(tx));
                    }

                    break;
                default:
                    throw new XunitException($"Unknown status '{status}'");
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MergePolicyBuildSucceededOpen(bool checksSuccessful)
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
                PolicyObject = new SubscriptionPolicy
                {
                    MergePolicy = MergePolicy.BuildSucceeded,
                    UpdateFrequency = UpdateFrequency.EveryBuild
                }
            };
            var repoInstallation = new RepoInstallation
            {
                Repository = "target.repo",
                InstallationId = 1,
            };
            await Context.RepoInstallations.AddAsync(repoInstallation);
            await Context.BuildChannels.AddAsync(buildChannel);
            await Context.Subscriptions.AddAsync(subscription);
            await Context.SaveChangesAsync();

            var pr = "https://pr.url/1";
            using (ITransaction tx = StateManager.CreateTransaction())
            {
                await PullRequests.AddAsync(
                    tx,
                    pr,
                    new InProgressPullRequest {BuildId = build.Id, SubscriptionId = subscription.Id, Url = pr});
                await PullRequestsBySubscription.AddAsync(tx, subscription.Id, pr);
                await tx.CommitAsync();
            }

            Darc.Setup(d => d.GetPullRequestStatusAsync(pr)).ReturnsAsync(PrStatus.Open);
            Darc.Setup(d => d.GetPullRequestChecksAsync(pr))
                .ReturnsAsync(
                    (string _) =>
                    {
                        if (checksSuccessful)
                        {
                            return new[]
                            {
                                new Check(CheckStatus.Succeeded, "n", "u"),
                                new Check(CheckStatus.Succeeded, "n2", "u2")
                            };
                        }

                        return new[]
                        {
                            new Check(CheckStatus.Succeeded, "n", "u"),
                            new Check(CheckStatus.Failed, "n2", "u2")
                        };
                    });
            if (checksSuccessful)
            {
                Darc.Setup(d => d.MergePullRequestAsync(pr, null, null, null, null)).Returns(Task.CompletedTask);
            }

            var updater = ActivatorUtilities.CreateInstance<DependencyUpdater>(Scope.ServiceProvider);
            await updater.CheckInProgressPRsAsync(CancellationToken.None);

            if (checksSuccessful)
            {
                Assert.Equal(build.Id, Context.Subscriptions.First().LastAppliedBuildId);
                using (ITransaction tx = StateManager.CreateTransaction())
                {
                    Assert.Equal(0, await PullRequests.GetCountAsync(tx));
                    Assert.Equal(0, await PullRequestsBySubscription.GetCountAsync(tx));
                }
            }
            else
            {
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
                }
            }
        }

        [Fact]
        public async Task NothingInProgress()
        {
            var updater = ActivatorUtilities.CreateInstance<DependencyUpdater>(Scope.ServiceProvider);
            await updater.CheckInProgressPRsAsync(CancellationToken.None);

            using (ITransaction tx = StateManager.CreateTransaction())
            {
                Assert.Equal(0, await PullRequests.GetCountAsync(tx));
                Assert.Equal(0, await PullRequestsBySubscription.GetCountAsync(tx));
            }
        }
    }
}
