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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

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

    public class DarcAsset
    {
        public DarcAsset(string name, string version, string sha, string source)
        {
            Name = name;
            Version = version;
            Sha = sha;
            Source = source;
        }

        public string Name { get; }
        public string Version { get; }
        public string Sha { get; }
        public string Source { get; }
    }

    public interface IDarc
    {
        Task<string> CreatePrAsync(string repository, string branch, IList<DarcAsset> assets);
        Task UpdatePrAsync(string pullRequest, string repository, string branch, IList<DarcAsset> assets);
        Task<PrStatus> GetPrStatusAsync(string pullRequest);
        Task MergePrAsync(string pullRequest);
        Task<IReadOnlyList<Check>> GetPrChecksAsync(string pullRequest);
    }

    public class Check
    {
        public Check(CheckStatus status, string name, string url)
        {
            Status = status;
            Name = name;
            Url = url;
        }

        public CheckStatus Status { get; }
        public string Name { get; }
        public string Url { get; }
    }

    public enum CheckStatus
    {
        None,
        Pending,
        Failed,
        Succeeded
    }

    public enum PrStatus
    {
        None,
        Open,
        Closed,
        Merged
    }

    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    public sealed class DependencyUpdater : IServiceImplementation, IDependencyUpdater
    {
        public IReliableStateManager StateManager { get; }
        public ILogger<DependencyUpdater> Logger { get; }
        public BuildAssetRegistryContext Context { get; }
        public IDarc Darc { get; }

        public DependencyUpdater(IReliableStateManager stateManager, ILogger<DependencyUpdater> logger, BuildAssetRegistryContext context, IDarc darc)
        {
            StateManager = stateManager;
            Logger = logger;
            Context = context;
            Darc = darc;
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
            var subscriptionsToUpdate =
                from sub in Context.Subscriptions
                where sub.PolicyUpdateFrequency == UpdateFrequency.EveryDay
                let latestBuild =
                    sub.Channel.BuildChannels.Select(bc => bc.Build)
                        .Where(b => b.Repository == sub.SourceRepository)
                        .OrderByDescending(b => b.DateProduced)
                        .FirstOrDefault()
                where latestBuild != null
                where sub.LastAppliedBuildId == null || sub.LastAppliedBuildId != latestBuild.Id
                select new { subscription = sub, latestBuild };

            foreach (var s in await subscriptionsToUpdate.ToListAsync(cancellationToken))
            {
                await Context.Entry(s.latestBuild).Collection(b => b.Assets).LoadAsync(cancellationToken);
                await UpdateSubscriptionAsync(s.subscription, s.latestBuild);
            }
        }

        /// <summary>
        ///   Check pull requests that are in progress every 5 minutes
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        [CronSchedule("0 0/5 * 1/1 * ? *", TimeZones.UTC)]
        public async Task CheckInProgressPRsAsync(CancellationToken cancellationToken)
        {
            var pullRequests =
                await StateManager.GetOrAddAsync<IReliableDictionary<string, InProgressPullRequest>>("pullRequests");
            var pullRequestsBySubscription =
                await StateManager.GetOrAddAsync<IReliableDictionary<int, string>>("pullRequestsBySubscription");

            using (Logger.BeginScope("Checking In Progress Pull Requests"))
            using (var tx = StateManager.CreateTransaction())
            {
                await pullRequests.ForEachAsync(tx, cancellationToken,
                    async (url, info) =>
                    {
                        var subscription = await Context.Subscriptions.FindAsync(info.SubscriptionId);
                        MergePolicy policy = subscription?.Policy.MergePolicy ?? MergePolicy.Never;
                        var status = await Darc.GetPrStatusAsync(url);
                        switch (status)
                        {
                            case PrStatus.Open:
                                switch (policy)
                                {
                                    case MergePolicy.Never:
                                        return;
                                    case MergePolicy.BuildSucceeded:
                                    case MergePolicy.UnitTestPassed: // for now both of these cases are the same
                                        var checks = await Darc.GetPrChecksAsync(url);
                                        if (checks.All(c => c.Status == CheckStatus.Succeeded))
                                        {
                                            await Darc.MergePrAsync(url);
                                            goto merged;
                                        }

                                        return;
                                    default:
                                        Logger.LogError("Unknown merge policy '{policy}'", policy);
                                        return;
                                }
                            case PrStatus.Merged:
                                merged:
                                if (subscription != null)
                                {
                                    subscription.LastAppliedBuildId = info.BuildId;
                                    await Context.SaveChangesAsync(cancellationToken);
                                }
                                goto case PrStatus.Closed;
                            case PrStatus.Closed:
                                await pullRequests.TryRemoveAsync(tx, url);
                                await pullRequestsBySubscription.TryRemoveAsync(tx, info.SubscriptionId);
                                return;
                            default:
                                Logger.LogError("Unknown pr status '{status}'", status);
                                return;

                        }
                    });
                await tx.CommitAsync();
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
            var subscriptionsToUpdate = await Context.Subscriptions
                .Where(sub => sub.ChannelId == channelId)
                .Where(sub => sub.SourceRepository == build.Repository)
                .Where(sub => sub.PolicyUpdateFrequency == UpdateFrequency.EveryBuild)
                .ToListAsync();
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
            var pullRequests =
                await StateManager.GetOrAddAsync<IReliableDictionary<string, InProgressPullRequest>>("pullRequests");
            var pullRequestsBySubscription =
                await StateManager.GetOrAddAsync<IReliableDictionary<int, string>>("pullRequestsBySubscription");

            using (Logger.BeginScope(
                "Updating subscription '{subscriptionId}' with build '{buildId}'",
                subscription.Id,
                build.Id))
            using (var tx = StateManager.CreateTransaction())
            {
                var targetRepository = subscription.TargetRepository;
                var targetBranch = subscription.TargetBranch;
                var assets = build.Assets
                    .Select(
                        a => new DarcAsset(
                            a.Name,
                            a.Version,
                            build.Commit,
                            a.Locations.First(l => l.Type == LocationType.NugetFeed).Location))
                    .ToList();

                var possiblePrUrl = await pullRequestsBySubscription.TryGetValueAsync(tx, subscription.Id);
                if (possiblePrUrl.HasValue)
                {
                    var prUrl = possiblePrUrl.Value;
                    var existingPr = (await pullRequests.TryGetValueAsync(tx, prUrl)).Value;
                    await Darc.UpdatePrAsync(existingPr.Url, targetRepository, targetBranch, assets);
                    var newPr = new InProgressPullRequest
                    {
                        Url = existingPr.Url,
                        BuildId = build.Id,
                        SubscriptionId = subscription.Id,
                    };
                    await pullRequests.SetAsync(tx, prUrl, newPr);
                    // update existing PR
                }
                else
                {
                    var prUrl = await Darc.CreatePrAsync(targetRepository, targetBranch, assets);
                    var newPr = new InProgressPullRequest
                    {
                        Url = prUrl,
                        BuildId = build.Id,
                        SubscriptionId = subscription.Id,
                    };
                    await pullRequests.SetAsync(tx, prUrl, newPr);
                    await pullRequestsBySubscription.SetAsync(tx, subscription.Id, prUrl);
                    // create new PR
                }

                await tx.CommitAsync();
            }
        }
    }

    [DataContract]
    public class InProgressPullRequest
    {
        [DataMember]
        public string Url { get; set; }

        [DataMember]
        public int SubscriptionId { get; set; }

        [DataMember]
        public int BuildId { get; set; }
    }
}
