// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Maestro.Data;
using Maestro.Data.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.ServiceFabric.ServiceHost.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Moq;
using ServiceFabricMocks;
using Xunit;
using Channel = Maestro.Data.Models.Channel;
using MergePolicy = Maestro.Data.Models.MergePolicy;
using SubscriptionPolicy = Maestro.Data.Models.SubscriptionPolicy;
using UpdateFrequency = Maestro.Data.Models.UpdateFrequency;

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

    public class SubscriptionActorTests : IDisposable
    {
        private readonly Lazy<BuildAssetRegistryContext> _context;
        protected readonly Mock<IRemote> Darc;
        protected readonly Mock<IHostingEnvironment> Env;
        protected readonly ServiceProvider Provider;
        protected readonly MockReminderManager Reminders;
        protected readonly IServiceScope Scope;
        protected readonly MockActorStateManager StateManager;

        public SubscriptionActorTests()
        {
            SubscriptionActor.CatchAllExceptions = false;

            var services = new ServiceCollection();
            StateManager = new MockActorStateManager();
            Darc = new Mock<IRemote>(MockBehavior.Strict);
            Env = new Mock<IHostingEnvironment>(MockBehavior.Strict);
            Reminders = new MockReminderManager();
            services.AddSingleton(Env.Object);
            services.AddSingleton<IActorStateManager>(StateManager);
            services.AddSingleton<IReminderManager>(Reminders);
            services.AddLogging();
            services.AddDbContext<BuildAssetRegistryContext>(
                options => { options.UseInMemoryDatabase("BuildAssetRegistry"); });
            services.AddSingleton(
                Mock.Of<IDarcRemoteFactory>(
                    f => f.CreateAsync(It.IsAny<string>(), It.IsAny<long>()) == Task.FromResult(Darc.Object)));
            Provider = services.BuildServiceProvider();
            Scope = Provider.CreateScope();

            _context = new Lazy<BuildAssetRegistryContext>(GetContext);
        }

        public BuildAssetRegistryContext Context => _context.Value;

        public void Dispose()
        {
            Darc.VerifyAll();
            Env.VerifyAll();
            Scope.Dispose();
            Provider.Dispose();
        }

        private BuildAssetRegistryContext GetContext()
        {
            return Scope.ServiceProvider.GetRequiredService<BuildAssetRegistryContext>();
        }

        private Channel CreateChannel()
        {
            return new Channel {Name = "channel", Classification = "class"};
        }

        private Build CreateOldBuild()
        {
            return new Build
            {
                Branch = "source.branch",
                Repository = "source.repo",
                BuildNumber = "old.build.number",
                Commit = "oldSha",
                DateProduced = DateTimeOffset.UtcNow.AddDays(-2)
            };
        }

        private Build CreateNewBuild()
        {
            var location = "https://source.feed/index.json";
            return new Build
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
        }

        private Subscription CreateSubscription(MergePolicy mergePolicy)
        {
            return new Subscription
            {
                SourceRepository = "source.repo",
                TargetRepository = "target.repo",
                TargetBranch = "target.branch",
                PolicyObject =
                    new SubscriptionPolicy {MergePolicy = mergePolicy, UpdateFrequency = UpdateFrequency.EveryDay}
            };
        }

        [Theory]
        [InlineData(PrStatus.Open, false, false)]
        [InlineData(PrStatus.Open, false, true)]
        [InlineData(PrStatus.Open, true, false)]
        [InlineData(PrStatus.Open, true, true)]
        [InlineData(PrStatus.Merged, false, false)]
        [InlineData(PrStatus.Merged, false, true)]
        [InlineData(PrStatus.Merged, true, false)]
        [InlineData(PrStatus.Merged, true, true)]
        [InlineData(PrStatus.Closed, false, false)]
        [InlineData(PrStatus.Closed, false, true)]
        [InlineData(PrStatus.Closed, true, false)]
        [InlineData(PrStatus.Closed, true, true)]
        public async Task SynchronizeInProgressPRAsync(
            PrStatus prStatus,
            bool existingPrHasChecks,
            bool existingPrPassedChecks)
        {
            Channel channel = CreateChannel();
            Build oldBuild = CreateOldBuild();
            Build build = CreateNewBuild();
            var buildChannels = new[]
            {
                new BuildChannel {Build = oldBuild, Channel = channel},
                new BuildChannel {Build = build, Channel = channel}
            };
            Subscription subscription = CreateSubscription(MergePolicy.BuildSucceeded);
            subscription.Channel = channel;
            var repoInstallation =
                new RepoInstallation {Repository = subscription.TargetRepository, InstallationId = 1};
            await Context.RepoInstallations.AddAsync(repoInstallation);
            await Context.Subscriptions.AddAsync(subscription);
            await Context.BuildChannels.AddRangeAsync(buildChannels);
            await Context.SaveChangesAsync();

            var existingPr = "https://repo.pr/existing";
            var actorId = new ActorId(subscription.Id);

            StateManager.Data[SubscriptionActor.PullRequest] =
                new InProgressPullRequest {BuildId = oldBuild.Id, Url = existingPr};

            Darc.Setup(d => d.GetPullRequestStatusAsync(existingPr)).ReturnsAsync(prStatus);

            if (prStatus == PrStatus.Open)
            {
                if (existingPrHasChecks)
                {
                    Darc.Setup(d => d.GetPullRequestChecksAsync(existingPr))
                        .ReturnsAsync(
                            new List<Check>
                            {
                                new Check(
                                    existingPrPassedChecks ? CheckState.Success : CheckState.Failure,
                                    "check",
                                    "https://check.stuff/1")
                            });
                }
                else
                {
                    Darc.Setup(d => d.GetPullRequestChecksAsync(existingPr)).ReturnsAsync(new List<Check>());
                }

                if (existingPrHasChecks && existingPrPassedChecks)
                {
                    Darc.Setup(d => d.MergePullRequestAsync(existingPr, null, null, null, null))
                        .Returns(Task.CompletedTask);
                }
            }

            var actor = ActivatorUtilities.CreateInstance<SubscriptionActor>(Scope.ServiceProvider, actorId);
            await actor.ReceiveReminderAsync(
                SubscriptionActor.PullRequestCheck,
                Array.Empty<byte>(),
                TimeSpan.Zero,
                TimeSpan.FromMinutes(5));

            switch (prStatus)
            {
                case PrStatus.Merged:
                    subscription.LastAppliedBuildId.Should().Be(oldBuild.Id);
                    goto case PrStatus.Closed;
                case PrStatus.Closed:
                    Reminders.Data.Should().BeEmpty();
                    StateManager.Data.Should().BeEmpty();
                    break;
            }
        }

        [Theory]
        [InlineData(MergePolicy.Never, PrStatus.None, false, false)]
        [InlineData(MergePolicy.Never, PrStatus.Open, false, false)]
        [InlineData(MergePolicy.Never, PrStatus.Merged, false, false)]
        [InlineData(MergePolicy.Never, PrStatus.Closed, false, false)]
        [InlineData(MergePolicy.BuildSucceeded, PrStatus.None, false, false)]
        [InlineData(MergePolicy.BuildSucceeded, PrStatus.Open, false, false)]
        [InlineData(MergePolicy.BuildSucceeded, PrStatus.Open, false, true)]
        [InlineData(MergePolicy.BuildSucceeded, PrStatus.Open, true, false)]
        [InlineData(MergePolicy.BuildSucceeded, PrStatus.Open, true, true)]
        [InlineData(MergePolicy.BuildSucceeded, PrStatus.Merged, false, false)]
        [InlineData(MergePolicy.BuildSucceeded, PrStatus.Merged, false, true)]
        [InlineData(MergePolicy.BuildSucceeded, PrStatus.Merged, true, false)]
        [InlineData(MergePolicy.BuildSucceeded, PrStatus.Merged, true, true)]
        [InlineData(MergePolicy.BuildSucceeded, PrStatus.Closed, false, false)]
        [InlineData(MergePolicy.BuildSucceeded, PrStatus.Closed, false, true)]
        [InlineData(MergePolicy.BuildSucceeded, PrStatus.Closed, true, false)]
        [InlineData(MergePolicy.BuildSucceeded, PrStatus.Closed, true, true)]
        public async Task Test(
            MergePolicy mergePolicy,
            PrStatus prStatus,
            bool existingPrHasChecks,
            bool existingPrPassedChecks)
        {
            Channel channel = CreateChannel();
            Build oldBuild = CreateOldBuild();
            Build build = CreateNewBuild();
            var buildChannels = new[]
            {
                new BuildChannel {Build = oldBuild, Channel = channel},
                new BuildChannel {Build = build, Channel = channel}
            };
            Subscription subscription = CreateSubscription(mergePolicy);
            subscription.Channel = channel;
            var repoInstallation =
                new RepoInstallation {Repository = subscription.TargetRepository, InstallationId = 1};
            Asset asset = build.Assets[0];
            await Context.RepoInstallations.AddAsync(repoInstallation);
            await Context.Subscriptions.AddAsync(subscription);
            await Context.BuildChannels.AddRangeAsync(buildChannels);
            await Context.SaveChangesAsync();

            var actorId = new ActorId(subscription.Id);
            var existingPr = "https://repo.pr/existing";
            var pr = "https://repo.pr/new";


            bool shouldMergeExistingPr = prStatus == PrStatus.Open && mergePolicy == MergePolicy.BuildSucceeded &&
                                         existingPrHasChecks && existingPrPassedChecks;

            void SetupCreatePr()
            {
                Darc.Setup(
                        d => d.CreatePullRequestAsync(
                            subscription.TargetRepository,
                            subscription.TargetBranch,
                            build.Commit,
                            It.IsAny<IList<AssetData>>(),
                            null,
                            It.IsAny<string>(),
                            It.IsAny<string>()))
                    .ReturnsAsync(
                        (
                            string repo,
                            string branch,
                            string commit,
                            IList<AssetData> assets,
                            string baseBranch,
                            string title,
                            string description) =>
                        {
                            assets.Should().BeEquivalentTo(new AssetData {Name = asset.Name, Version = asset.Version});
                            return pr;
                        });
            }

            void SetupUpdatePr()
            {
                Darc.Setup(
                        r => r.UpdatePullRequestAsync(
                            existingPr,
                            build.Commit,
                            subscription.TargetBranch,
                            It.IsAny<IList<AssetData>>(),
                            It.IsAny<string>(),
                            It.IsAny<string>()))
                    .ReturnsAsync(
                        (
                            string url,
                            string commit,
                            string branch,
                            IList<AssetData> assets,
                            string title,
                            string description) =>
                        {
                            return url;
                        });
            }

            void SetupExistingPr()
            {
                StateManager.Data[SubscriptionActor.PullRequest] =
                    new InProgressPullRequest {BuildId = oldBuild.Id, Url = existingPr};
                Darc.Setup(r => r.GetPullRequestStatusAsync(existingPr)).ReturnsAsync(prStatus);
                if (mergePolicy == MergePolicy.BuildSucceeded && prStatus == PrStatus.Open)
                {
                    if (existingPrHasChecks)
                    {
                        Darc.Setup(r => r.GetPullRequestChecksAsync(existingPr))
                            .ReturnsAsync(
                                new List<Check>
                                {
                                    new Check(
                                        existingPrPassedChecks ? CheckState.Success : CheckState.Failure,
                                        "check",
                                        "https://check.stuff/1")
                                });
                    }
                    else
                    {
                        Darc.Setup(r => r.GetPullRequestChecksAsync(existingPr)).ReturnsAsync(new List<Check>());
                    }
                }

                if (shouldMergeExistingPr)
                {
                    Darc.Setup(r => r.MergePullRequestAsync(existingPr, null, null, null, null))
                        .Returns(Task.CompletedTask);
                }
            }

            switch (prStatus)
            {
                case PrStatus.None:
                    SetupCreatePr();
                    break;
                case PrStatus.Open:
                    SetupExistingPr();
                    if (shouldMergeExistingPr)
                    {
                        SetupCreatePr();
                    }
                    else
                    {
                        SetupUpdatePr();
                    }

                    break;
                case PrStatus.Merged:
                    SetupExistingPr();
                    SetupCreatePr();
                    break;
                case PrStatus.Closed:
                    SetupExistingPr();
                    SetupCreatePr();
                    break;
            }


            var actor = ActivatorUtilities.CreateInstance<SubscriptionActor>(Scope.ServiceProvider, actorId);
            await actor.UpdateAsync(build.Id);

            if (shouldMergeExistingPr || prStatus == PrStatus.Merged)
            {
                subscription.LastAppliedBuildId.Should().Be(oldBuild.Id);
            }
            else
            {
                subscription.LastAppliedBuildId.Should().Be(null);
            }

            StateManager.Data.Should()
                .BeEquivalentTo(
                    new Dictionary<string, object>
                    {
                        [SubscriptionActor.PullRequest] = new InProgressPullRequest
                        {
                            BuildId = build.Id,
                            Url = prStatus == PrStatus.Open && !shouldMergeExistingPr ? existingPr : pr
                        }
                    });

            Reminders.Data.Should()
                .BeEquivalentTo(
                    new Dictionary<string, MockReminderManager.Reminder>
                    {
                        [SubscriptionActor.PullRequestCheck] = new MockReminderManager.Reminder(
                            SubscriptionActor.PullRequestCheck,
                            Array.Empty<byte>(),
                            TimeSpan.FromMinutes(5),
                            TimeSpan.FromMinutes(5))
                    });
        }
    }
}
