// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DependencyUpdater;
using Maestro.Contracts;
using Maestro.Data;
using Maestro.Data.Models;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.ServiceFabric.ServiceHost.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Data;
using MergePolicy = Maestro.Data.Models.MergePolicy;

namespace SubscriptionActorService
{
    namespace unused
    {
        // class needed to appease service fabric build time generation of actor code
        [StatePersistence(StatePersistence.Persisted)]
        public class SubscriptionActor : Actor, ISubscriptionActor, IRemindable
        {
            public SubscriptionActor(ActorService actorService, ActorId actorId) : base(actorService, actorId)
            {
            }

            public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
            {
                throw new NotImplementedException();
            }

            public Task SynchronizeInProgressPRAsync()
            {
                throw new NotImplementedException();
            }

            public Task UpdateAsync(int buildId)
            {
                throw new NotImplementedException();
            }
        }
    }

    public class SubscriptionActor : ISubscriptionActor, IRemindable
    {
        public const string PullRequestCheck = "pullRequestCheck";
        public const string PullRequest = "pullRequest";

        public SubscriptionActor(
            IActorStateManager stateManager,
            ActorId id,
            IReminderManager reminders,
            BuildAssetRegistryContext context,
            IDarcRemoteFactory darcFactory,
            ILogger<SubscriptionActor> logger)
        {
            StateManager = stateManager;
            Id = id;
            Reminders = reminders;
            Context = context;
            DarcFactory = darcFactory;
            Logger = logger;
        }

        public IActorStateManager StateManager { get; }
        public ActorId Id { get; }
        public IReminderManager Reminders { get; }
        public BuildAssetRegistryContext Context { get; }
        public IDarcRemoteFactory DarcFactory { get; }
        public ILogger<SubscriptionActor> Logger { get; }

        public Guid SubscriptionId => Id.GetGuidId();

        public async Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
        {
            if (reminderName == PullRequestCheck)
            {
                await SynchronizeInProgressPRAsync();
            }
            else
            {
                throw new ReminderNotFoundException(reminderName);
            }
        }

        public async Task UpdateAsync(int buildId)
        {
            await SynchronizeInProgressPRAsync();

            Subscription subscription = await Context.Subscriptions.FindAsync(SubscriptionId);
            Build build = await Context.Builds.Include(b => b.Assets)
                .ThenInclude(a => a.Locations)
                .FirstAsync(b => b.Id == buildId);

            string targetRepository = subscription.TargetRepository;
            string targetBranch = subscription.TargetBranch;
            long installationId = await Context.GetInstallationId(subscription.TargetRepository);
            IRemote darc = await DarcFactory.CreateAsync(targetRepository, installationId);
            List<AssetData> assets = build.Assets.Select(a => new AssetData {Name = a.Name, Version = a.Version})
                .ToList();

            ConditionalValue<InProgressPullRequest> maybePr =
                await StateManager.TryGetStateAsync<InProgressPullRequest>(PullRequest);
            string prUrl;
            if (maybePr.HasValue)
            {
                InProgressPullRequest pr = maybePr.Value;
                await darc.UpdatePullRequestAsync(pr.Url, build.Commit, targetBranch, assets);
                prUrl = pr.Url;
            }
            else
            {
                prUrl = await darc.CreatePullRequestAsync(targetRepository, targetBranch, build.Commit, assets);
            }

            var newPr = new InProgressPullRequest {Url = prUrl, BuildId = build.Id};
            await StateManager.SetStateAsync(PullRequest, newPr);
            await Reminders.TryRegisterReminderAsync(
                PullRequestCheck,
                Array.Empty<byte>(),
                new TimeSpan(0, 5, 0),
                new TimeSpan(0, 5, 0));
            await StateManager.SaveStateAsync();
        }

        public async Task SynchronizeInProgressPRAsync()
        {
            Subscription subscription = await Context.Subscriptions.FindAsync(SubscriptionId);
            if (subscription == null)
            {
                await Reminders.TryUnregisterReminderAsync(PullRequestCheck);
                await StateManager.TryRemoveStateAsync(PullRequest);
                return;
            }

            ConditionalValue<InProgressPullRequest> maybePr =
                await StateManager.TryGetStateAsync<InProgressPullRequest>(PullRequest);
            if (maybePr.HasValue)
            {
                InProgressPullRequest pr = maybePr.Value;
                long installationId = await Context.GetInstallationId(subscription.TargetRepository);
                IRemote darc = await DarcFactory.CreateAsync(pr.Url, installationId);
                MergePolicy policy = subscription.PolicyObject.MergePolicy;
                PrStatus status = await darc.GetPullRequestStatusAsync(pr.Url);
                switch (status)
                {
                    case PrStatus.Open:
                        switch (policy)
                        {
                            case MergePolicy.Never:
                                return;
                            case MergePolicy.BuildSucceeded:
                            case MergePolicy.UnitTestPassed: // for now both of these cases are the same
                                if (await ShouldMergePrAsync(darc, pr.Url, policy))
                                {
                                    await darc.MergePullRequestAsync(pr.Url);
                                    goto merged;
                                }

                                return;
                            default:
                                Logger.LogError("Unknown merge policy '{policy}'", policy);
                                return;
                        }
                    case PrStatus.Merged:
                        merged:
                        subscription.LastAppliedBuildId = pr.BuildId;
                        await Context.SaveChangesAsync();

                        goto case PrStatus.Closed;
                    case PrStatus.Closed:
                        await StateManager.RemoveStateAsync(PullRequest);
                        break;
                    default:
                        Logger.LogError("Unknown pr status '{status}'", status);
                        return;
                }
            }

            await Reminders.TryUnregisterReminderAsync(PullRequestCheck);
        }

        private async Task<bool> ShouldMergePrAsync(IRemote darc, string url, MergePolicy policy)
        {
            IList<Check> checks = await darc.GetPullRequestChecksAsync(url);
            if (checks.Count == 0)
            {
                return false; // Don't auto merge anything that has no checks.
            }

            if (checks.All(c => c.Status == CheckStatus.Succeeded))
            {
                return true; // If every check succeeded merge the pr
            }

            return false;
        }
    }
}
