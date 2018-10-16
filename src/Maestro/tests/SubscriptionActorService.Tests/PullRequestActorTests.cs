// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using FluentAssertions;
using Maestro.Contracts;
using Maestro.Data;
using Maestro.Data.Models;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Actors;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using Xunit;
using Asset = Maestro.Contracts.Asset;

namespace SubscriptionActorService.Tests
{
    public class PullRequestActorTests : SubscriptionOrPullRequestActorTests
    {
        private const long InstallationId = 1174;
        private const string InProgressPrUrl = "https://github.com/owner/repo/pull/10";
        private const string InProgressPrHeadBranch = "pr.head.branch";

        private readonly Dictionary<(string repo, long installationId), Mock<IRemote>> DarcRemotes =
            new Dictionary<(string repo, long installationId), Mock<IRemote>>();

        private readonly Mock<IMergePolicyEvaluator> MergePolicyEvaluator;

        private readonly Dictionary<ActorId, Mock<ISubscriptionActor>> SubscriptionActors =
            new Dictionary<ActorId, Mock<ISubscriptionActor>>();

        private string NewBranch;

        public PullRequestActorTests()
        {
            Builder.RegisterInstance(
                (Func<ActorId, ISubscriptionActor>) (actorId =>
                {
                    Mock<ISubscriptionActor> mock = SubscriptionActors.GetOrAddValue(
                        actorId,
                        CreateMock<ISubscriptionActor>);
                    return mock.Object;
                }));

            MergePolicyEvaluator = CreateMock<IMergePolicyEvaluator>();
            Builder.RegisterInstance(MergePolicyEvaluator.Object);

            var remoteFactory = new Mock<IDarcRemoteFactory>(MockBehavior.Strict);
            remoteFactory.Setup(f => f.CreateAsync(It.IsAny<string>(), It.IsAny<long>()))
                .ReturnsAsync(
                    (string repo, long installationId) =>
                        DarcRemotes.GetOrAddValue((repo, installationId), CreateMock<IRemote>).Object);
            Builder.RegisterInstance(remoteFactory.Object);
        }

        protected override Task BeforeExecute(IComponentContext context)
        {
            var dbContext = context.Resolve<BuildAssetRegistryContext>();
            dbContext.Repositories.Add(
                new Repository
                {
                    RepositoryName = TargetRepo,
                    InstallationId = InstallationId
                });
            return base.BeforeExecute(context);
        }

        private void ThenGetRequiredUpdatesShouldHaveBeenCalled(Build withBuild)
        {
            var assets = new List<IEnumerable<AssetData>>();
            DarcRemotes[(TargetRepo, InstallationId)]
                .Verify(r => r.GetRequiredUpdatesAsync(TargetRepo, TargetBranch, NewCommit, Capture.In(assets)));
            assets.Should()
                .BeEquivalentTo(
                    new List<List<AssetData>>
                    {
                        withBuild.Assets.Select(
                                a => new AssetData
                                {
                                    Name = a.Name,
                                    Version = a.Version
                                })
                            .ToList()
                    });
        }

        private void AndCreateNewBranchShouldHaveBeenCalled()
        {
            var captureNewBranch = new CaptureMatch<string>(newBranch => NewBranch = newBranch);
            DarcRemotes[(TargetRepo, InstallationId)]
                .Verify(r => r.CreateNewBranchAsync(TargetRepo, TargetBranch, Capture.With(captureNewBranch)));
        }

        private void AndCommitUpdatesShouldHaveBeenCalled(Build withUpdatesFromBuild)
        {
            var updatedDependencies = new List<List<DependencyDetail>>();
            DarcRemotes[(TargetRepo, InstallationId)]
                .Verify(
                    r => r.CommitUpdatesAsync(
                        TargetRepo,
                        NewBranch ?? InProgressPrHeadBranch,
                        Capture.In(updatedDependencies),
                        It.IsAny<string>()));
            updatedDependencies.Should()
                .BeEquivalentTo(
                    new List<List<DependencyDetail>>
                    {
                        withUpdatesFromBuild.Assets.Select(
                                a => new DependencyDetail
                                {
                                    Name = a.Name,
                                    Version = a.Version
                                })
                            .ToList()
                    });
        }

        private void AndCreatePullRequestShouldHaveBeenCalled()
        {
            var pullRequests = new List<PullRequest>();
            DarcRemotes[(TargetRepo, InstallationId)]
                .Verify(r => r.CreatePullRequestAsync(TargetRepo, Capture.In(pullRequests)));
            pullRequests.Should()
            .BeEquivalentTo(
                new List<PullRequest>
                {
                    new PullRequest
                    {
                        BaseBranch = TargetBranch,
                        HeadBranch = NewBranch
                    }
                },
                options => options.Excluding(pr => pr.Title).Excluding(pr => pr.Description));
        }

        private void AndUpdatePullRequestShouldHaveBeenCalled()
        {
            var pullRequests = new List<PullRequest>();
            DarcRemotes[(TargetRepo, InstallationId)]
                .Verify(r => r.UpdatePullRequestAsync(InProgressPrUrl, Capture.In(pullRequests)));
            pullRequests.Should()
            .BeEquivalentTo(
                new List<PullRequest>
                {
                    new PullRequest
                    {
                        BaseBranch = TargetBranch,
                        HeadBranch = NewBranch ?? InProgressPrHeadBranch
                    }
                },
                options => options.Excluding(pr => pr.Title).Excluding(pr => pr.Description));
        }

        private void AndSubscriptionShouldBeUpdatedForMergedPullRequest(Build withBuild)
        {
            SubscriptionActors[new ActorId(Subscription.Id)]
                .Verify(s => s.UpdateForMergedPullRequestAsync(withBuild.Id));
        }

        private void WithRequiredUpdates(Build fromBuild)
        {
            DarcRemotes.GetOrAddValue((TargetRepo, InstallationId), CreateMock<IRemote>)
                .Setup(
                    r => r.GetRequiredUpdatesAsync(
                        TargetRepo,
                        TargetBranch,
                        NewCommit,
                        It.IsAny<IEnumerable<AssetData>>()))
                .ReturnsAsync(
                    (string repo, string branch, string sha, IEnumerable<AssetData> assets) =>
                    {
                        return assets.Select(
                                d => new DependencyDetail
                                {
                                    Name = d.Name,
                                    Version = d.Version
                                })
                            .ToList();
                    });
        }

        private IDisposable WithExistingPullRequest(bool updatable)
        {
            var pr = new InProgressPullRequest
            {
                Url = InProgressPrUrl,
                ContainedSubscriptions = new List<SubscriptionPullRequestUpdate>
                {
                    new SubscriptionPullRequestUpdate
                    {
                        BuildId = -1,
                        SubscriptionId = Subscription.Id
                    }
                }
            };
            StateManager.SetStateAsync(PullRequestActorImplementation.PullRequest, pr);
            ExpectedActorState.Add(PullRequestActorImplementation.PullRequest, pr);

            ActionRunner.Setup(r => r.ExecuteAction(It.IsAny<Expression<Func<Task<ActionResult<bool?>>>>>()))
                .ReturnsAsync(updatable);

            if (updatable)
            {
                DarcRemotes.GetOrAddValue((TargetRepo, InstallationId), CreateMock<IRemote>)
                    .Setup(r => r.GetPullRequestAsync(InProgressPrUrl))
                    .ReturnsAsync(
                        new PullRequest
                        {
                            HeadBranch = InProgressPrHeadBranch,
                            BaseBranch = TargetBranch
                        });
            }

            return Disposable.Create(
                () =>
                {
                    ActionRunner.Verify(r => r.ExecuteAction(It.IsAny<Expression<Func<Task<ActionResult<bool?>>>>>()));
                    if (updatable)
                    {
                        DarcRemotes[(TargetRepo, InstallationId)].Verify(r => r.GetPullRequestAsync(InProgressPrUrl));
                    }
                });
        }

        private void AndShouldHavePullRequestCheckReminder()
        {
            ExpectedReminders.Add(
                PullRequestActorImplementation.PullRequestCheck,
                new MockReminderManager.Reminder(
                    PullRequestActorImplementation.PullRequestCheck,
                    null,
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(5)));
        }

        private void ThenShouldHavePullRequestUpdateReminder()
        {
            ExpectedReminders.Add(
                PullRequestActorImplementation.PullRequestUpdate,
                new MockReminderManager.Reminder(
                    PullRequestActorImplementation.PullRequestUpdate,
                    Array.Empty<byte>(),
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(5)));
        }

        private void AndShouldHaveInProgressPullRequestState(Build forBuild)
        {
            ExpectedActorState.Add(
                PullRequestActorImplementation.PullRequest,
                new InProgressPullRequest
                {
                    ContainedSubscriptions = new List<SubscriptionPullRequestUpdate>
                    {
                        new SubscriptionPullRequestUpdate
                        {
                            BuildId = forBuild.Id,
                            SubscriptionId = Subscription.Id
                        }
                    }
                });
        }

        private void AndShouldHavePendingUpdateState(Build forBuild)
        {
            ExpectedActorState.Add(
                PullRequestActorImplementation.PullRequestUpdate,
                new List<PullRequestActorImplementation.UpdateAssetsParameters>
                {
                    new PullRequestActorImplementation.UpdateAssetsParameters
                    {
                        SubscriptionId = Subscription.Id,
                        BuildId = forBuild.Id,
                        SourceSha = forBuild.Commit,
                        Assets = forBuild.Assets.Select(
                                a => new Asset
                                {
                                    Name = a.Name,
                                    Version = a.Version
                                })
                            .ToList()
                    }
                });
        }

        private PullRequestActor CreateActor(IComponentContext context)
        {
            var provider = new AutofacServiceProvider(context);
            ActorId actorId;
            if (Subscription.PolicyObject.Batchable)
            {
                actorId = PullRequestActorId.Create(Subscription.TargetRepository, Subscription.TargetBranch);
            }
            else
            {
                actorId = new ActorId(Subscription.Id);
            }

            return ActivatorUtilities.CreateInstance<PullRequestActor>(provider, actorId);
        }

        public class ProcessPendingUpdatesAsync : PullRequestActorTests
        {
            private async Task WhenProcessPendingUpdatesAsyncIsCalled()
            {
                await Execute(
                    async context =>
                    {
                        PullRequestActor actor = CreateActor(context);
                        await actor.Implementation.ProcessPendingUpdatesAsync();
                    });
            }

            private void GivenAPendingUpdateReminder()
            {
                var reminder = new MockReminderManager.Reminder(
                    PullRequestActorImplementation.PullRequestUpdate,
                    Array.Empty<byte>(),
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(5));
                Reminders.Data[PullRequestActorImplementation.PullRequestUpdate] = reminder;
                ExpectedReminders[PullRequestActorImplementation.PullRequestUpdate] = reminder;
            }

            private void AndNoPendingUpdates()
            {
                var updates = new List<PullRequestActorImplementation.UpdateAssetsParameters>();
                StateManager.Data[PullRequestActorImplementation.PullRequestUpdate] = updates;
                ExpectedActorState[PullRequestActorImplementation.PullRequestUpdate] = updates;
            }

            private void AndPendingUpdates(Build forBuild)
            {
                var updates = new List<PullRequestActorImplementation.UpdateAssetsParameters>
                {
                    new PullRequestActorImplementation.UpdateAssetsParameters
                    {
                        SubscriptionId = Subscription.Id,
                        BuildId = forBuild.Id,
                        SourceSha = forBuild.Commit,
                        Assets = forBuild.Assets.Select(
                                a => new Asset
                                {
                                    Name = a.Name,
                                    Version = a.Version
                                })
                            .ToList()
                    }
                };
                StateManager.Data[PullRequestActorImplementation.PullRequestUpdate] = updates;
                ExpectedActorState[PullRequestActorImplementation.PullRequestUpdate] = updates;
            }

            private void ThenUpdateReminderIsRemoved()
            {
                ExpectedReminders.Remove(PullRequestActorImplementation.PullRequestUpdate);
            }

            private void AndPendingUpdateIsRemoved()
            {
                ExpectedActorState.Remove(PullRequestActorImplementation.PullRequestUpdate);
            }

            [Fact]
            public async Task NoPendingUpdates()
            {
                GivenATestChannel();
                GivenASubscription(
                    new SubscriptionPolicy
                    {
                        Batchable = true,
                        UpdateFrequency = UpdateFrequency.EveryBuild
                    });
                Build b = GivenANewBuild();

                GivenAPendingUpdateReminder();
                AndNoPendingUpdates();
                await WhenProcessPendingUpdatesAsyncIsCalled();
                ThenUpdateReminderIsRemoved();
            }

            [Fact]
            public async Task PendingUpdatesNotUpdatablePr()
            {
                GivenATestChannel();
                GivenASubscription(
                    new SubscriptionPolicy
                    {
                        Batchable = true,
                        UpdateFrequency = UpdateFrequency.EveryBuild
                    });
                Build b = GivenANewBuild();

                GivenAPendingUpdateReminder();
                AndPendingUpdates(b);
                using (WithExistingPullRequest(false))
                {
                    await WhenProcessPendingUpdatesAsyncIsCalled();
                    // Nothing happens
                }
            }

            [Fact]
            public async Task PendingUpdatesUpdatablePr()
            {
                GivenATestChannel();
                GivenASubscription(
                    new SubscriptionPolicy
                    {
                        Batchable = true,
                        UpdateFrequency = UpdateFrequency.EveryBuild
                    });
                Build b = GivenANewBuild();

                GivenAPendingUpdateReminder();
                AndPendingUpdates(b);
                WithRequiredUpdates(b);
                using (WithExistingPullRequest(true))
                {
                    await WhenProcessPendingUpdatesAsyncIsCalled();
                    ThenUpdateReminderIsRemoved();
                    AndPendingUpdateIsRemoved();
                    ThenGetRequiredUpdatesShouldHaveBeenCalled(b);
                    AndCommitUpdatesShouldHaveBeenCalled(b);
                    AndUpdatePullRequestShouldHaveBeenCalled();
                    AndShouldHavePullRequestCheckReminder();
                }
            }
        }

        public class UpdateAssetsAsync : PullRequestActorTests
        {
            private async Task WhenUpdateAssetsAsyncIsCalled(Build forBuild)
            {
                await Execute(
                    async context =>
                    {
                        PullRequestActor actor = CreateActor(context);
                        await actor.Implementation.UpdateAssetsAsync(
                            Subscription.Id,
                            forBuild.Id,
                            NewCommit,
                            forBuild.Assets.Select(
                                    a => new Asset
                                    {
                                        Name = a.Name,
                                        Version = a.Version
                                    })
                                .ToList());
                    });
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task UpdateWithAssetsNoExistingPR(bool batchable)
            {
                GivenATestChannel();
                GivenASubscription(
                    new SubscriptionPolicy
                    {
                        Batchable = batchable,
                        UpdateFrequency = UpdateFrequency.EveryBuild
                    });
                Build b = GivenANewBuild();

                WithRequiredUpdates(b);

                await WhenUpdateAssetsAsyncIsCalled(b);

                ThenGetRequiredUpdatesShouldHaveBeenCalled(b);
                AndCreateNewBranchShouldHaveBeenCalled();
                AndCommitUpdatesShouldHaveBeenCalled(b);
                AndCreatePullRequestShouldHaveBeenCalled();
                AndShouldHavePullRequestCheckReminder();
                AndShouldHaveInProgressPullRequestState(b);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task UpdateWithAssetsExistingPR(bool batchable)
            {
                GivenATestChannel();
                GivenASubscription(
                    new SubscriptionPolicy
                    {
                        Batchable = batchable,
                        UpdateFrequency = UpdateFrequency.EveryBuild
                    });
                Build b = GivenANewBuild();

                WithRequiredUpdates(b);
                using (WithExistingPullRequest(true))
                {
                    await WhenUpdateAssetsAsyncIsCalled(b);

                    ThenGetRequiredUpdatesShouldHaveBeenCalled(b);
                    AndCommitUpdatesShouldHaveBeenCalled(b);
                    AndUpdatePullRequestShouldHaveBeenCalled();
                    AndShouldHavePullRequestCheckReminder();
                }
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task UpdateWithAssetsExistingPRNotUpdatable(bool batchable)
            {
                GivenATestChannel();
                GivenASubscription(
                    new SubscriptionPolicy
                    {
                        Batchable = batchable,
                        UpdateFrequency = UpdateFrequency.EveryBuild
                    });
                Build b = GivenANewBuild();

                WithRequiredUpdates(b);
                using (WithExistingPullRequest(false))
                {
                    await WhenUpdateAssetsAsyncIsCalled(b);

                    ThenShouldHavePullRequestUpdateReminder();
                    AndShouldHavePendingUpdateState(b);
                }
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task UpdateWithNoAssets(bool batchable)
            {
                GivenATestChannel();
                GivenASubscription(
                    new SubscriptionPolicy
                    {
                        Batchable = batchable,
                        UpdateFrequency = UpdateFrequency.EveryBuild
                    });
                Build b = GivenANewBuild(Array.Empty<(string, string)>());

                WithRequiredUpdates(b);

                await WhenUpdateAssetsAsyncIsCalled(b);

                ThenGetRequiredUpdatesShouldHaveBeenCalled(b);
                AndSubscriptionShouldBeUpdatedForMergedPullRequest(b);
            }
        }
    }
}
