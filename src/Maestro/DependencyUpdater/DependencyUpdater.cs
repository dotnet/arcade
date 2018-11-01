// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Maestro.Contracts;
using Maestro.Data;
using Maestro.Data.Models;
using Microsoft.DotNet.ServiceFabric.ServiceHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;

namespace DependencyUpdater
{
    [DataContract]
    public class DependencyUpdateItem
    {
        [DataMember]
        public int BuildId { get; set; }

        [DataMember]
        public int ChannelId { get; set; }
    }

    /// <summary>
    ///     An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    public sealed class DependencyUpdater : IServiceImplementation, IDependencyUpdater
    {
        public DependencyUpdater(
            IReliableStateManager stateManager,
            ILogger<DependencyUpdater> logger,
            BuildAssetRegistryContext context,
            Func<ActorId, ISubscriptionActor> subscriptionActorFactory)
        {
            StateManager = stateManager;
            Logger = logger;
            Context = context;
            SubscriptionActorFactory = subscriptionActorFactory;
        }

        public IReliableStateManager StateManager { get; }
        public ILogger<DependencyUpdater> Logger { get; }
        public BuildAssetRegistryContext Context { get; }
        public Func<ActorId, ISubscriptionActor> SubscriptionActorFactory { get; }

        public async Task StartUpdateDependenciesAsync(int buildId, int channelId)
        {
            IReliableConcurrentQueue<DependencyUpdateItem> queue =
                await StateManager.GetOrAddAsync<IReliableConcurrentQueue<DependencyUpdateItem>>("queue");
            using (ITransaction tx = StateManager.CreateTransaction())
            {
                await queue.EnqueueAsync(
                    tx,
                    new DependencyUpdateItem
                    {
                        BuildId = buildId,
                        ChannelId = channelId
                    });
                await tx.CommitAsync();
            }
        }

        /// <summary>
        ///     Run a single subscription
        /// </summary>
        /// <param name="subscriptionId">Subscription to run the update for.</param>
        /// <returns></returns>
        public Task StartSubscriptionUpdateAsync(Guid subscriptionId)
        {
            var subscriptionToUpdate = (from sub in Context.Subscriptions
                                         where sub.Id == subscriptionId
                                         where sub.Enabled
                                         let latestBuild =
                                             sub.Channel.BuildChannels.Select(bc => bc.Build)
                                                 .Where(b => b.Repository == sub.SourceRepository)
                                                 .OrderByDescending(b => b.DateProduced)
                                                 .FirstOrDefault()
                                         where latestBuild != null
                                         where sub.LastAppliedBuildId == null || sub.LastAppliedBuildId != latestBuild.Id
                                         select new
                                         {
                                             subscription = sub.Id,
                                             latestBuild = latestBuild.Id
                                         }).SingleOrDefault();

            if (subscriptionToUpdate != null)
            {
                return UpdateSubscriptionAsync(subscriptionToUpdate.subscription, subscriptionToUpdate.latestBuild);
            }
            return Task.CompletedTask;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            IReliableConcurrentQueue<DependencyUpdateItem> queue =
                await StateManager.GetOrAddAsync<IReliableConcurrentQueue<DependencyUpdateItem>>("queue");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (ITransaction tx = StateManager.CreateTransaction())
                    {
                        ConditionalValue<DependencyUpdateItem> maybeItem = await queue.TryDequeueAsync(
                            tx,
                            cancellationToken);
                        if (maybeItem.HasValue)
                        {
                            DependencyUpdateItem item = maybeItem.Value;
                            using (Logger.BeginScope(
                                "Processing dependency update for build {buildId} in channel {channelId}",
                                item.BuildId,
                                item.ChannelId))
                            {
                                await UpdateDependenciesAsync(item.BuildId, item.ChannelId);
                            }
                        }

                        await tx.CommitAsync();
                    }

                    await Task.Delay(1000, cancellationToken);
                }
                catch (TaskCanceledException tcex) when (tcex.CancellationToken == cancellationToken)
                {
                    // ignore
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Processing queue messages");
                }
            }
        }

        /// <summary>
        ///     Check "EveryDay" subscriptions every day at 5 AM
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [CronSchedule("0 0 5 1/1 * ? *", TimeZones.PST)]
        public async Task CheckSubscriptionsAsync(CancellationToken cancellationToken)
        {
            var subscriptionsToUpdate = from sub in Context.Subscriptions
                where sub.Enabled
                let updateFrequency = JsonExtensions.JsonValue(sub.PolicyString, "lax $.UpdateFrequency")
                where updateFrequency == ((int) UpdateFrequency.EveryDay).ToString()
                let latestBuild =
                    sub.Channel.BuildChannels.Select(bc => bc.Build)
                        .Where(b => b.Repository == sub.SourceRepository)
                        .OrderByDescending(b => b.DateProduced)
                        .FirstOrDefault()
                where latestBuild != null
                where sub.LastAppliedBuildId == null || sub.LastAppliedBuildId != latestBuild.Id
                select new
                {
                    subscription = sub.Id,
                    latestBuild = latestBuild.Id
                };

            foreach (var s in await subscriptionsToUpdate.ToListAsync(cancellationToken))
            {
                await UpdateSubscriptionAsync(s.subscription, s.latestBuild);
            }
        }

        /// <summary>
        ///     Update dependencies for a new build in a channel
        /// </summary>
        /// <param name="buildId"></param>
        /// <param name="channelId"></param>
        /// <returns></returns>
        public async Task UpdateDependenciesAsync(int buildId, int channelId)
        {
            Build build = await Context.Builds.FindAsync(buildId);
            List<Subscription> subscriptionsToUpdate = await (from sub in Context.Subscriptions
                where sub.Enabled
                where sub.ChannelId == channelId
                where sub.SourceRepository == build.Repository
                let updateFrequency = JsonExtensions.JsonValue(sub.PolicyString, "lax $.UpdateFrequency")
                where updateFrequency == ((int) UpdateFrequency.EveryBuild).ToString()
                select sub).ToListAsync();
            if (!subscriptionsToUpdate.Any())
            {
                return;
            }

            await Task.WhenAll(subscriptionsToUpdate.Select(sub => UpdateSubscriptionAsync(sub.Id, buildId)));
        }

        private async Task UpdateSubscriptionAsync(Guid subscriptionId, int buildId)
        {
            using (Logger.BeginScope(
                "Updating subscription '{subscriptionId}' with build '{buildId}'",
                subscriptionId,
                buildId))
            {
                ISubscriptionActor actor = SubscriptionActorFactory(new ActorId(subscriptionId));
                await actor.UpdateAsync(buildId);
            }
        }
    }
}
