// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Internal;
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

            public Task UpdateAsync(int buildId)
            {
                throw new NotImplementedException();
            }

            public Task<string> CheckMergePolicyAsync(string prUrl)
            {
                throw new NotImplementedException();
            }

            public Task<string> RunAction(string action, params object[] arguments)
            {
                throw new NotImplementedException();
            }
        }
    }

    public class SubscriptionActor : ISubscriptionActor, IRemindable
    {
        public const string PullRequestCheck = "pullRequestCheck";
        public const string PullRequest = "pullRequest";


        /// <summary>
        ///   Hook for tests to disable the catch (Exception) blocks to allow test exceptions out
        /// </summary>
        public static bool CatchAllExceptions = true;

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

        public Task<string> RunAction(string action, params object[] arguments)
        {
            Func<Task<string>> run;
            string messageFormat;
            switch (action)
            {
                case nameof(UpdateAsync):
                    var buildId = (int)arguments[0];
                    run = () => UpdateAsyncImpl(buildId);
                    messageFormat = "Updating subscription for build '{buildId}'";
                    break;
                case nameof(CheckMergePolicyAsync):
                    var prUrl = (string) arguments[0];
                    run = () => CheckMergePolicyAsyncImpl(prUrl);
                    messageFormat = "Checking merge policy for pr '{url}'";
                    break;
                default:
                    throw new ArgumentException($"The action '{action}' does not exist.", nameof(action));
            }

            return RunAction(run, action, messageFormat, arguments);
        }

        private async Task<string> RunAction(Func<Task<string>> run, string method, string messageFormat, params object[] arguments)
        {
            using (Logger.BeginScope(messageFormat, arguments))
            {
                try
                {
                    var result = await run();
                    await TrackSubscriptionUpdateSuccess(result, messageFormat, arguments);
                    return result;
                }
                catch (SubscriptionException subex)
                {
                    await TrackSubscriptionUpdateFailure(subex.Message, method, messageFormat, arguments);
                }
                catch (Exception ex) when (CatchAllExceptions)
                {
                    Logger.LogError(ex,  "Unexpected error Processing update: {errorMessage}", ex.Message);
                    await TrackSubscriptionUpdateFailure(ex.Message, method, messageFormat, arguments);
                }
            }

            return null;
        }

        private async Task<string> UpdateAsyncImpl(int buildId)
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
            List<AssetData> assets = build.Assets
                .Select(a => new AssetData { Name = a.Name, Version = a.Version })
                .ToList();
            string title = GetTitle(subscription, build);
            string description = GetDescription(subscription, build, assets);

            ConditionalValue<InProgressPullRequest> maybePr =
                await StateManager.TryGetStateAsync<InProgressPullRequest>(PullRequest);
            string prUrl;
            if (maybePr.HasValue)
            {
                InProgressPullRequest pr = maybePr.Value;
                await darc.UpdatePullRequestAsync(
                    pr.Url,
                    build.Commit,
                    targetBranch,
                    assets,
                    title,
                    description);
                prUrl = pr.Url;
            }
            else
            {
                prUrl = await darc.CreatePullRequestAsync(
                    targetRepository,
                    targetBranch,
                    build.Commit,
                    assets,
                    pullRequestTitle: title,
                    pullRequestDescription: description);

                if (string.IsNullOrEmpty(prUrl))
                {
                    return $"No Pull request created. Darc Reports no dependencies need to be updated.";
                }
            }

            var newPr = new InProgressPullRequest { Url = prUrl, BuildId = build.Id };
            await StateManager.SetStateAsync(PullRequest, newPr);
            await Reminders.TryRegisterReminderAsync(
                PullRequestCheck,
                Array.Empty<byte>(),
                new TimeSpan(0, 5, 0),
                new TimeSpan(0, 5, 0));
            await StateManager.SaveStateAsync();

            return $"Pull request '{prUrl}' updated.";
        }

        public Task UpdateAsync(int buildId)
        {
            return RunAction(nameof(UpdateAsync), buildId);
        }

        private async Task<string> CheckMergePolicyAsyncImpl(string prUrl)
        {
            Subscription subscription = await Context.Subscriptions.FindAsync(SubscriptionId);
            if (subscription == null)
            {
                await Reminders.TryUnregisterReminderAsync(PullRequestCheck);
                await StateManager.TryRemoveStateAsync(PullRequest);
                return "Action Ignored: Subscription does not exist.";
            }
            ConditionalValue<InProgressPullRequest> maybePr =
                await StateManager.TryGetStateAsync<InProgressPullRequest>(PullRequest);
            if (!maybePr.HasValue)
            {
                return "Action Ignored: Pull Request not found.";
            }

            InProgressPullRequest pr = maybePr.Value;
            long installationId = await Context.GetInstallationId(subscription.TargetRepository);
            IRemote darc = await DarcFactory.CreateAsync(pr.Url, installationId);
            MergePolicy policy = subscription.PolicyObject.MergePolicy;
            PrStatus status = await darc.GetPullRequestStatusAsync(pr.Url);
            switch (status)
            {
                case PrStatus.Open:
                    var result = await CheckMergePolicyInternalAsync(darc, policy, pr);
                    if (result.StartsWith("Merged:"))
                    {
                        subscription.LastAppliedBuildId = pr.BuildId;
                        await Context.SaveChangesAsync();
                        await StateManager.RemoveStateAsync(PullRequest);
                        return result;
                    }

                    return result;
                default:
                    return "Action Ignored: Pull Request is not Open.";
            }
        }

        private async Task<string> CheckMergePolicyInternalAsync(
            IRemote darc,
            MergePolicy policy,
            InProgressPullRequest pr)
        {
            switch (policy)
            {
                case MergePolicy.Never:
                    return NotMergedFailedPolicy(policy, pr.Url);
                case MergePolicy.BuildSucceeded:
                case MergePolicy.UnitTestPassed: // for now both of these cases are the same
                    if (await ShouldMergePrAsync(darc, pr.Url, policy))
                    {
                        await darc.MergePullRequestAsync(pr.Url);
                        return Merged(policy, pr.Url);
                    }

                    return NotMergedFailedPolicy(policy, pr.Url);
                default:
                    Logger.LogError("Unknown merge policy '{policy}'", policy);
                    return NotMergedUnknownPolicy(policy, pr.Url);
            }
        }

        private string Merged(MergePolicy policy, string url)
        {
            return $"Merged: PR '{url}' passed policy {policy}";
        }

        private string NotMergedFailedPolicy(MergePolicy policy, string url)
        {
            return $"NOT Merged: PR '{url}' failed policy {policy}";
        }

        private string NotMergedUnknownPolicy(MergePolicy policy, string url)
        {
            return $"NOT Merged: PR '{url}' unknown policy {policy}";
        }

        public Task<string> CheckMergePolicyAsync(string prUrl)
        {
            return RunAction(nameof(CheckMergePolicyAsync), prUrl);
        }

        private string GetTitle(Subscription subscription, Build build)
        {
            return $"Update dependencies from build {build.BuildNumber} of {build.Repository}";
        }

        private string GetDescription(Subscription subscription, Build build, List<AssetData> assets)
        {
            return $@"This change updates the dependencies from {build.Repository} to the following

{string.Join(@"
", assets.Select(a => $"- {a.Name} - {a.Version}"))}";
        }

        private async Task TrackSubscriptionUpdateSuccess(string result, string messageFormat, params object[] arguments)
        {
            SubscriptionUpdate subscriptionUpdate = await GetSubscriptionUpdate();

            subscriptionUpdate.Action = new FormattedLogValues(messageFormat, arguments).ToString();
            subscriptionUpdate.ErrorMessage = result;
            subscriptionUpdate.Method = null;
            subscriptionUpdate.Arguments = null;
            subscriptionUpdate.Success = true;
            await Context.SaveChangesAsync();
        }

        private async Task TrackSubscriptionUpdateFailure(string errorMessage, string method, string messageFormat, params object[] arguments)
        {
            SubscriptionUpdate subscriptionUpdate = await GetSubscriptionUpdate();

            subscriptionUpdate.Action = new FormattedLogValues(messageFormat, arguments).ToString();
            subscriptionUpdate.ErrorMessage = errorMessage;
            subscriptionUpdate.Method = method;
            subscriptionUpdate.Arguments = JsonConvert.SerializeObject(arguments);
            subscriptionUpdate.Success = false;
            await Context.SaveChangesAsync();
        }

        private async Task<SubscriptionUpdate> GetSubscriptionUpdate()
        {
            var subscriptionUpdate = await Context.SubscriptionUpdates.FindAsync(SubscriptionId);
            if (subscriptionUpdate == null)
            {
                Context.SubscriptionUpdates.Add(subscriptionUpdate = new SubscriptionUpdate { SubscriptionId = SubscriptionId });
            }
            else
            {
                Context.SubscriptionUpdates.Update(subscriptionUpdate);
            }

            return subscriptionUpdate;
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
                if (string.IsNullOrEmpty(pr.Url))
                {
                    // somehow a bad PR got in the collection, remove it
                    await StateManager.RemoveStateAsync(PullRequest);
                    return;
                }
                long installationId = await Context.GetInstallationId(subscription.TargetRepository);
                IRemote darc = await DarcFactory.CreateAsync(pr.Url, installationId);
                MergePolicy policy = subscription.PolicyObject.MergePolicy;
                PrStatus status = await darc.GetPullRequestStatusAsync(pr.Url);
                switch (status)
                {
                    case PrStatus.Open:
                        var result = await RunAction(() => CheckMergePolicyInternalAsync(darc, policy, pr), nameof(CheckMergePolicyAsync),  "Checking merge policy for pr '{url}'", pr.Url);
                        if (result != null && result.StartsWith("Merged:"))
                        {
                            goto case PrStatus.Merged;
                        }

                        return;
                    case PrStatus.Merged:
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

            if (checks.All(c => c.Status == CheckState.Success))
            {
                return true; // If every check succeeded merge the pr
            }

            return false;
        }
    }
}
