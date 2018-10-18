// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Contracts;
using Maestro.Data;
using Maestro.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Asset = Maestro.Contracts.Asset;

namespace SubscriptionActorService
{
    namespace unused
    {
        // class needed to appease service fabric build time generation of actor code
        [StatePersistence(StatePersistence.Persisted)]
        public class SubscriptionActor : Actor, ISubscriptionActor
        {
            public SubscriptionActor(ActorService actorService, ActorId actorId) : base(actorService, actorId)
            {
            }

            public Task<string> RunActionAsync(string method, string arguments)
            {
                throw new NotImplementedException();
            }

            public Task UpdateAsync(int buildId)
            {
                throw new NotImplementedException();
            }

            public Task UpdateForMergedPullRequestAsync(int updateBuildId)
            {
                throw new NotImplementedException();
            }
        }
    }

    public class SubscriptionActor : ISubscriptionActor, IActionTracker
    {
        public SubscriptionActor(
            IActorStateManager stateManager,
            ActorId id,
            BuildAssetRegistryContext context,
            ILogger<SubscriptionActor> logger,
            IActionRunner actionRunner,
            Func<ActorId, IPullRequestActor> pullRequestActorFactory)
        {
            StateManager = stateManager;
            Id = id;
            Context = context;
            Logger = logger;
            ActionRunner = actionRunner;
            PullRequestActorFactory = pullRequestActorFactory;
        }


        public IActorStateManager StateManager { get; }
        public ActorId Id { get; }
        public BuildAssetRegistryContext Context { get; }
        public ILogger<SubscriptionActor> Logger { get; }
        public IActionRunner ActionRunner { get; }
        public Func<ActorId, IPullRequestActor> PullRequestActorFactory { get; }

        public Guid SubscriptionId => Id.GetGuidId();

        public async Task TrackSuccessfulAction(string action, string result)
        {
            SubscriptionUpdate subscriptionUpdate = await GetSubscriptionUpdate();

            subscriptionUpdate.Action = action;
            subscriptionUpdate.ErrorMessage = result;
            subscriptionUpdate.Method = null;
            subscriptionUpdate.Arguments = null;
            subscriptionUpdate.Success = true;
            await Context.SaveChangesAsync();
        }

        public async Task TrackFailedAction(string action, string result, string method, string arguments)
        {
            SubscriptionUpdate subscriptionUpdate = await GetSubscriptionUpdate();

            subscriptionUpdate.Action = action;
            subscriptionUpdate.ErrorMessage = result;
            subscriptionUpdate.Method = method;
            subscriptionUpdate.Arguments = arguments;
            subscriptionUpdate.Success = false;
            await Context.SaveChangesAsync();
        }

        public Task<string> RunActionAsync(string method, string arguments)
        {
            return ActionRunner.RunAction(this, method, arguments);
        }

        Task ISubscriptionActor.UpdateAsync(int buildId)
        {
            return ActionRunner.ExecuteAction(() => UpdateAsync(buildId));
        }

        public async Task UpdateForMergedPullRequestAsync(int updateBuildId)
        {
            Subscription subscription = await Context.Subscriptions.FindAsync(SubscriptionId);
            subscription.LastAppliedBuildId = updateBuildId;
            Context.Subscriptions.Update(subscription);
            await Context.SaveChangesAsync();
        }


        [ActionMethod("Updating subscription for build {buildId}")]
        public async Task<ActionResult<bool>> UpdateAsync(int buildId)
        {
            Subscription subscription = await Context.Subscriptions.FindAsync(SubscriptionId);

            Build build = await Context.Builds.Include(b => b.Assets)
                .ThenInclude(a => a.Locations)
                .FirstAsync(b => b.Id == buildId);

            ActorId pullRequestActorId;

            if (subscription.PolicyObject.Batchable)
            {
                pullRequestActorId = PullRequestActorId.Create(
                    subscription.TargetRepository,
                    subscription.TargetBranch);
            }
            else
            {
                pullRequestActorId = PullRequestActorId.Create(SubscriptionId);
            }

            IPullRequestActor pullRequestActor = PullRequestActorFactory(pullRequestActorId);

            List<Asset> assets = build.Assets.Select(
                    a => new Asset
                    {
                        Name = a.Name,
                        Version = a.Version
                    })
                .ToList();

            await pullRequestActor.UpdateAssetsAsync(SubscriptionId, build.Id, build.Commit, assets);

            return ActionResult.Create(true, "Update Sent");
        }

        private async Task<SubscriptionUpdate> GetSubscriptionUpdate()
        {
            SubscriptionUpdate subscriptionUpdate = await Context.SubscriptionUpdates.FindAsync(SubscriptionId);
            if (subscriptionUpdate == null)
            {
                Context.SubscriptionUpdates.Add(
                    subscriptionUpdate = new SubscriptionUpdate {SubscriptionId = SubscriptionId});
            }
            else
            {
                Context.SubscriptionUpdates.Update(subscriptionUpdate);
            }

            return subscriptionUpdate;
        }
    }
}
