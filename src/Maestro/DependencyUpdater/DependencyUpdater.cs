using Maestro.Contracts;
using Maestro.Data;
using Maestro.Data.Models;
using Microsoft.DotNet.ServiceFabric.ServiceHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Helix.ServiceHost;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.DarcLib;
using MergePolicy = Maestro.Data.Models.MergePolicy;
using UpdateFrequency = Maestro.Data.Models.UpdateFrequency;
using Microsoft.ServiceFabric.Actors;

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
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    public sealed class DependencyUpdater : IServiceImplementation, IDependencyUpdater
    {
        public IReliableStateManager StateManager { get; }
        public ILogger<DependencyUpdater> Logger { get; }
        public BuildAssetRegistryContext Context { get; }
        public Func<ActorId, ISubscriptionActor> SubscriptionActorFactory { get; }

        public DependencyUpdater(
            IReliableStateManager stateManager,
            ILogger<DependencyUpdater> logger,
            BuildAssetRegistryContext context,
            Func<ActorId, ISubscriptionActor> subscriptionActorFactory
            )
        {
            StateManager = stateManager;
            Logger = logger;
            Context = context;
            SubscriptionActorFactory = subscriptionActorFactory;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            IReliableConcurrentQueue<DependencyUpdateItem> queue = await StateManager.GetOrAddAsync<IReliableConcurrentQueue<DependencyUpdateItem>>("queue");
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (ITransaction tx = StateManager.CreateTransaction())
                    {
                        ConditionalValue<DependencyUpdateItem> maybeItem = await queue.TryDequeueAsync(tx, cancellationToken);
                        if (maybeItem.HasValue)
                        {
                            DependencyUpdateItem item = maybeItem.Value;
                            using (Logger.BeginScope("Processing dependency update for build {buildId} in channel {channelId}", item.BuildId, item.ChannelId))
                            {
                                await UpdateDependenciesAsync(item.BuildId, item.ChannelId);
                            }
                        }

                        await tx.CommitAsync();
                    }

                    await Task.Delay(1000, cancellationToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Processing queue messages");
                }
            }
        }

        public async Task StartUpdateDependenciesAsync(int buildId, int channelId)
        {
            IReliableConcurrentQueue<DependencyUpdateItem> queue = await StateManager.GetOrAddAsync<IReliableConcurrentQueue<DependencyUpdateItem>>("queue");
            using (ITransaction tx = StateManager.CreateTransaction())
            {
                await queue.EnqueueAsync(tx, new DependencyUpdateItem
                {
                    BuildId = buildId,
                    ChannelId = channelId,
                });
                await tx.CommitAsync();
            }
        }

        /// <summary>
        ///   Check "EveryDay" subscriptions every day at 5 AM
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [CronSchedule("0 0 5 1/1 * ? *", TimeZones.PST)]
        public async Task CheckSubscriptionsAsync(CancellationToken cancellationToken)
        {
            var subscriptionsToUpdate = from sub in Context.Subscriptions
                let updateFrequency = JsonExtensions.JsonValue(sub.PolicyString, "lax $.UpdateFrequency")
                where updateFrequency == ((int) UpdateFrequency.EveryDay).ToString()
                let latestBuild =
                    sub.Channel.BuildChannels.Select(bc => bc.Build)
                        .Where(b => b.Repository == sub.SourceRepository)
                        .OrderByDescending(b => b.DateProduced)
                        .FirstOrDefault()
                where latestBuild != null
                where sub.LastAppliedBuildId == null || sub.LastAppliedBuildId != latestBuild.Id
                select new {subscription = sub, latestBuild};

            foreach (var s in await subscriptionsToUpdate.ToListAsync(cancellationToken))
            {
                await Context.Entry(s.latestBuild).Collection(b => b.Assets).LoadAsync(cancellationToken);
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
            var build = await Context.Builds.FindAsync(buildId);
            var subscriptionsToUpdate = await (
                from sub in Context.Subscriptions
                where sub.ChannelId == channelId
                where sub.SourceRepository == build.Repository
                let updateFrequency = JsonExtensions.JsonValue(sub.PolicyString, "lax $.UpdateFrequency")
                where updateFrequency == ((int)UpdateFrequency.EveryBuild).ToString()
                select sub).ToListAsync();
            if (!subscriptionsToUpdate.Any())
            {
                return;
            }

            var fullBuild = await Context.Builds.Include(b => b.Assets)
                .ThenInclude(a => a.Locations)
                .FirstAsync(b => b.Id == buildId);
            await Task.WhenAll(subscriptionsToUpdate.Select(sub => UpdateSubscriptionAsync(sub, fullBuild)));
        }

        private async Task UpdateSubscriptionAsync(Subscription subscription, Build build)
        {
            using (Logger.BeginScope(
                "Updating subscription '{subscriptionId}' with build '{buildId}'",
                subscription.Id,
                build.Id))
            {
                var actor = SubscriptionActorFactory(new ActorId(subscription.Id));
                await actor.UpdateAsync(build.Id);
            }
        }
    }
}
