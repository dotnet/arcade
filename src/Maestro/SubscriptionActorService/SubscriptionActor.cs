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
using Maestro.MergePolicies;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.ServiceFabric.ServiceHost.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Internal;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Runtime;
using Microsoft.ServiceFabric.Data;
using Newtonsoft.Json;
using MergePolicy = Maestro.MergePolicies.MergePolicy;
using SubscriptionPolicy = Maestro.Data.Models.SubscriptionPolicy;

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

            public Task SubscriptionDeletedAsync(string user)
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

            public Task<string> RunAction(string action, string arguments)
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
        ///     Hook for tests to disable the catch (Exception) blocks to allow test exceptions out
        /// </summary>
        public static bool CatchAllExceptions = true;

        public SubscriptionActor(
            IActorStateManager stateManager,
            ActorId id,
            IReminderManager reminders,
            BuildAssetRegistryContext context,
            IDarcRemoteFactory darcFactory,
            IEnumerable<MergePolicy> policyEvaluators,
            ILogger<SubscriptionActor> logger)
        {
            StateManager = stateManager;
            Id = id;
            Reminders = reminders;
            Context = context;
            DarcFactory = darcFactory;
            Logger = logger;
            PolicyEvaluators = policyEvaluators.ToDictionary(p => p.Name);
        }

        public Dictionary<string, MergePolicy> PolicyEvaluators { get; }

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

        Task<string> ISubscriptionActor.RunAction(string action, string arguments)
        {
            return RunAction(action, DeserializeArguments(arguments));
        }

        public Task<string> RunAction(string action, params object[] arguments)
        {
            Func<Task<string>> run;
            string messageFormat;
            switch (action)
            {
                case nameof(UpdateAsync):
                    var buildId = Convert.ToInt32(arguments[0]);
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

        public async Task SubscriptionDeletedAsync(string user)
        {
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

                long installationId = 0;
                if (pr.Url.Contains("github.com"))
                {
                    (string owner, string repo, int id) = GitHubClient.ParsePullRequestUri(pr.Url);
                    installationId = await Context.GetInstallationId($"https://github.com/{owner}/{repo}");
                }

                IRemote darc = await DarcFactory.CreateAsync(pr.Url, installationId);
                await darc.CreatePullRequestCommentAsync(
                    pr.Url,
                    $@"The subscription that generated this pull request has been deleted by @{user}.
This pull request will no longer be tracked by maestro.");
                await StateManager.RemoveStateAsync(PullRequest);
            }
        }

        public Task UpdateAsync(int buildId)
        {
            return RunAction(nameof(UpdateAsync), buildId);
        }

        public Task<string> CheckMergePolicyAsync(string prUrl)
        {
            return RunAction(nameof(CheckMergePolicyAsync), prUrl);
        }

        private async Task<string> RunAction(
            Func<Task<string>> run,
            string method,
            string messageFormat,
            params object[] arguments)
        {
            using (Logger.BeginScope(messageFormat, arguments))
            {
                try
                {
                    string result = await run();
                    await TrackSubscriptionUpdateSuccess(result, messageFormat, arguments);
                    return result;
                }
                catch (SubscriptionException subex)
                {
                    await TrackSubscriptionUpdateFailure(subex.Message, method, messageFormat, arguments);
                }
                catch (DarcException darcEx)
                {
                    await TrackSubscriptionUpdateFailure(darcEx.Message, method, messageFormat, arguments);
                }
                catch (Exception ex) when (CatchAllExceptions)
                {
                    Logger.LogError(ex, "Unexpected error Processing update: {errorMessage}", ex.Message);
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
            List<AssetData> assets = build.Assets.Select(a => new AssetData {Name = a.Name, Version = a.Version})
                .ToList();
            string title = GetTitle(subscription, build);
            string description = GetDescription(subscription, build, assets);

            ConditionalValue<InProgressPullRequest> maybePr =
                await StateManager.TryGetStateAsync<InProgressPullRequest>(PullRequest);
            InProgressPullRequest pr;
            if (maybePr.HasValue)
            {
                pr = maybePr.Value;
                await darc.UpdatePullRequestAsync(pr.Url, build.Commit, targetBranch, assets, title, description);
            }
            else
            {
                string prUrl = await darc.CreatePullRequestAsync(
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

                pr = new InProgressPullRequest {Url = prUrl};
            }

            pr.BuildId = build.Id;
            await StateManager.SetStateAsync(PullRequest, pr);
            await Reminders.TryRegisterReminderAsync(
                PullRequestCheck,
                Array.Empty<byte>(),
                new TimeSpan(0, 5, 0),
                new TimeSpan(0, 5, 0));
            await StateManager.SaveStateAsync();

            return $"Pull request '{pr.Url}' updated.";
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
            SubscriptionPolicy policy = subscription.PolicyObject;
            PrStatus status = await darc.GetPullRequestStatusAsync(pr.Url);
            switch (status)
            {
                case PrStatus.Open:
                    string result = await CheckMergePolicyInternalAsync(darc, policy.MergePolicies, pr);
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
            IList<MergePolicyDefinition> policyDefinitions,
            InProgressPullRequest pr)
        {
            var results = new List<(MergePolicy policy, MergePolicyEvaluationResult result)>();
            if (policyDefinitions != null)
            {
                foreach (MergePolicyDefinition policyDefinition in policyDefinitions)
                {
                    var context = new MergePolicyEvaluationContext(pr.Url, darc, Logger, policyDefinition.Properties);
                    if (PolicyEvaluators.TryGetValue(policyDefinition.Name, out MergePolicy policyEvaluator))
                    {
                        MergePolicyEvaluationResult result = await policyEvaluator.EvaluateAsync(context);
                        results.Add((policyEvaluator, result));
                    }
                    else
                    {
                        results.Add((null, context.Fail($"Unknown Merge Policy '{policyDefinition.Name}'")));
                    }
                }
            }

            if (results.Count == 0)
            {
                await UpdateStatusCommentAsync(
                    darc,
                    pr,
                    $@"## Auto-Merge Status
This pull request will not be merged automatically because the subscription with id '{SubscriptionId}' does not have any merge policies.");
                return NotMergedNoPolicies();
            }

            string DisplayPolicy((MergePolicy policy, MergePolicyEvaluationResult result) p)
            {
                (MergePolicy policy, MergePolicyEvaluationResult result) = p;
                if (policy == null)
                {
                    return $"- ❌ **{result.Message}**";
                }

                if (result.Succeeded)
                {
                    return $"- ✔️ **{policy.DisplayName}** Succeeded";
                }

                return $"- ❌ **{policy.DisplayName}** {result.Message}";
            }

            if (results.Any(r => !r.result.Succeeded))
            {
                await UpdateStatusCommentAsync(
                    darc,
                    pr,
                    $@"## Auto-Merge Status
This pull request has not been merged because the subscription with id '{SubscriptionId}' is waiting on the following merge policies.

{string.Join("\n", results.OrderBy(r => r.policy == null ? " " : r.policy.Name).Select(DisplayPolicy))}");
                return NotMergedFailedPolicies(results.Where(r => !r.result.Succeeded), pr.Url);
            }

            await UpdateStatusCommentAsync(
                darc,
                pr,
                $@"## Auto-Merge Status
This pull request has been merged because the following merge policies have succeeded.

{string.Join("\n", results.OrderBy(r => r.policy == null ? " " : r.policy.Name).Select(DisplayPolicy))}");

            await darc.MergePullRequestAsync(pr.Url, null);
            return Merged(policyDefinitions, pr.Url);
        }

        private async Task UpdateStatusCommentAsync(IRemote darc, InProgressPullRequest pr, string message)
        {
            if (pr.StatusCommentId == null)
            {
                pr.StatusCommentId = await darc.CreatePullRequestCommentAsync(pr.Url, message);
                await StateManager.SetStateAsync(PullRequest, pr);
            }
            else
            {
                await darc.UpdatePullRequestCommentAsync(pr.Url, pr.StatusCommentId, message);
            }
        }

        private string Merged(IEnumerable<MergePolicyDefinition> policyDefinitions, string url)
        {
            return $"Merged: PR '{url}' passed policies {string.Join(", ", policyDefinitions.Select(p => p.Name))}";
        }

        private string NotMergedNoPolicies()
        {
            return $"NOT Merged: Subscription '{SubscriptionId}' has no merge policies.";
        }

        private string NotMergedFailedPolicies(
            IEnumerable<(MergePolicy policy, MergePolicyEvaluationResult result)> failedPolicies,
            string url)
        {
            return
                $"NOT Merged: PR '{url}' failed policies {string.Join(", ", failedPolicies.Select(p => p.policy.Name))}";
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

        private async Task TrackSubscriptionUpdateSuccess(
            string result,
            string messageFormat,
            params object[] arguments)
        {
            SubscriptionUpdate subscriptionUpdate = await GetSubscriptionUpdate();

            subscriptionUpdate.Action = new FormattedLogValues(messageFormat, arguments).ToString();
            subscriptionUpdate.ErrorMessage = result;
            subscriptionUpdate.Method = null;
            subscriptionUpdate.Arguments = null;
            subscriptionUpdate.Success = true;
            await Context.SaveChangesAsync();
        }

        private async Task TrackSubscriptionUpdateFailure(
            string errorMessage,
            string method,
            string messageFormat,
            params object[] arguments)
        {
            SubscriptionUpdate subscriptionUpdate = await GetSubscriptionUpdate();

            subscriptionUpdate.Action = new FormattedLogValues(messageFormat, arguments).ToString();
            subscriptionUpdate.ErrorMessage = errorMessage;
            subscriptionUpdate.Method = method;
            subscriptionUpdate.Arguments = SerializeArguments(arguments);
            subscriptionUpdate.Success = false;
            await Context.SaveChangesAsync();
        }

        private string SerializeArguments(object[] arguments)
        {
            return JsonConvert.SerializeObject(arguments);
        }

        private object[] DeserializeArguments(string value)
        {
            return JsonConvert.DeserializeObject<object[]>(value);
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
                SubscriptionPolicy policy = subscription.PolicyObject;
                PrStatus status = await darc.GetPullRequestStatusAsync(pr.Url);
                switch (status)
                {
                    case PrStatus.Open:
                        string result = await RunAction(
                            () => CheckMergePolicyInternalAsync(darc, policy.MergePolicies, pr),
                            nameof(CheckMergePolicyAsync),
                            "Checking merge policy for pr '{url}'",
                            pr.Url);
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

        private async Task<bool> ShouldMergePrAsync(IRemote darc, string url, MergePolicyDefinition policyDefinition)
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
