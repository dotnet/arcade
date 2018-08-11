using DependencyUpdater;
using Maestro.Contracts;
using Maestro.Data;
using Maestro.GitHub;
using Microsoft.AspNetCore.Hosting;
using Microsoft.DotNet.ServiceFabric.ServiceHost;
using Microsoft.DotNet.ServiceFabric.ServiceHost.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.DarcLib;
using MergePolicy = Maestro.Data.Models.MergePolicy;

namespace SubscriptionActorService
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static void Main()
        {
            ServiceHost.Run(
                host =>
                {
                    host.RegisterStatefulActorService<SubscriptionService, SubscriptionActor>("SubscriptionActor");
                    host.ConfigureServices(
                        services =>
                        {
                            services.AddSingleton<IDarcRemoteFactory, DarcRemoteFactory>();
                            services.AddGitHubTokenProvider();
                            services.AddSingleton(
                                provider => ServiceHostConfiguration.Get(
                                    provider.GetRequiredService<IHostingEnvironment>()));
                            services.AddDbContext<BuildAssetRegistryContext>(
                                (provider, options) =>
                                {
                                    var config = provider.GetRequiredService<IConfigurationRoot>();
                                    options.UseSqlServer(config.GetSection("BuildAssetRegistry")["ConnectionString"]);
                                });
                            services.Configure<GitHubTokenProviderOptions>(
                                (options, provider) =>
                                {
                                    var config = provider.GetRequiredService<IConfigurationRoot>();
                                    var section = config.GetSection("GitHub");
                                    section.Bind(options);
                                    options.ApplicationName = "Maestro";
                                    options.ApplicationVersion = Assembly.GetEntryAssembly()
                                        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                                        ?.InformationalVersion;
                                });
                        });
                });
        }
    }

    public class SubscriptionService : IServiceImplementation
    {
        public Task RunAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    namespace unused
    {
        // class needed to appease service fabric build time generation of actor code
        [StatePersistence(StatePersistence.Persisted)]
        public class SubscriptionActor : Actor, ISubscriptionActor, IRemindable
        {
            public SubscriptionActor(ActorService actorService, ActorId actorId) : base(actorService, actorId)
            {
            }

            public Task SynchronizeInProgressPRAsync()
            {
                throw new NotImplementedException();
            }

            public Task UpdateAsync(int buildId)
            {
                throw new NotImplementedException();
            }

            public Task ReceiveReminderAsync(string reminderName, byte[] state, TimeSpan dueTime, TimeSpan period)
            {
                throw new NotImplementedException();
            }
        }
    }

    public class SubscriptionActor : ISubscriptionActor, IRemindable
    {
        public const string PullRequestCheck = "pullRequestCheck";
        public const string PullRequest = "pullRequest";
        public IActorStateManager StateManager { get; }
        public ActorId Id { get; }
        public IReminderManager Reminders { get; }
        public BuildAssetRegistryContext Context { get; }
        public IDarcRemoteFactory DarcFactory { get; }
        public ILogger<SubscriptionActor> Logger { get; }

        public Guid SubscriptionId => Id.GetGuidId();

        public SubscriptionActor(IActorStateManager stateManager, ActorId id, IReminderManager reminders, BuildAssetRegistryContext context, IDarcRemoteFactory darcFactory, ILogger<SubscriptionActor> logger)
        {
            StateManager = stateManager;
            Id = id;
            Reminders = reminders;
            Context = context;
            DarcFactory = darcFactory;
            Logger = logger;
        }

        public async Task UpdateAsync(int buildId)
        {
            await SynchronizeInProgressPRAsync();

            var subscription = await Context.Subscriptions.FindAsync(SubscriptionId);
            var build = await Context.Builds.Include(b => b.Assets)
                .ThenInclude(a => a.Locations)
                .FirstAsync(b => b.Id == buildId);

            var targetRepository = subscription.TargetRepository;
            var targetBranch = subscription.TargetBranch;
            var installationId = await Context.GetInstallationId(subscription.TargetRepository);
            var darc = await DarcFactory.CreateAsync(targetRepository, installationId);
            var assets = build.Assets
                .Select(
                    a => new AssetData
                    {
                        Name = a.Name,
                        Version = a.Version,
                    })
                .ToList();

            var maybePr = await StateManager.TryGetStateAsync<InProgressPullRequest>(PullRequest);
            string prUrl;
            if (maybePr.HasValue)
            {
                var pr = maybePr.Value;
                await darc.UpdatePullRequestAsync(pr.Url, build.Commit, targetBranch, assets);
                prUrl = pr.Url;
            }
            else
            {
                prUrl = await darc.CreatePullRequestAsync(targetRepository, targetBranch, build.Commit, assets);
            }
            var newPr = new InProgressPullRequest
            {
                Url = prUrl,
                BuildId = build.Id,
            };
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
            var subscription = await Context.Subscriptions.FindAsync(SubscriptionId);
            if (subscription == null)
            {
                await Reminders.TryUnregisterReminderAsync(PullRequestCheck);
                await StateManager.TryRemoveStateAsync(PullRequest);
                return;
            }
            var maybePr = await StateManager.TryGetStateAsync<InProgressPullRequest>(PullRequest);
            if (maybePr.HasValue)
            {
                var pr = maybePr.Value;
                var installationId = await Context.GetInstallationId(subscription.TargetRepository);
                var darc = await DarcFactory.CreateAsync(pr.Url, installationId);
                MergePolicy policy = subscription.PolicyObject.MergePolicy;
                var status = await darc.GetPullRequestStatusAsync(pr.Url);
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
            var checks = await darc.GetPullRequestChecksAsync(url);
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
    }

    [DataContract]
    public class InProgressPullRequest
    {
        [DataMember]
        public string Url { get; set; }

        [DataMember]
        public int BuildId { get; set; }
    }
}
