// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Models.PopUps;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    class AddSubscriptionOperation : Operation
    {
        AddSubscriptionCommandLineOptions _options;

        public AddSubscriptionOperation(AddSubscriptionCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        /// <summary>
        /// Implements the 'add-subscription' operation
        /// </summary>
        /// <param name="options"></param>
        public override async Task<int> ExecuteAsync()
        {
            DarcSettings darcSettings = LocalSettings.GetDarcSettings(_options, Logger);
            // No need to set up a git type or PAT here.
            Remote remote = new Remote(darcSettings, Logger);

            if (_options.IgnoreChecks.Count() > 0 && !_options.AllChecksSuccessfulMergePolicy)
            {
                Logger.LogError($"--ignore-checks must be combined with --all-checks-passed");
                return Constants.ErrorCode;
            }
            // Parse the merge policies
            List<MergePolicy> mergePolicies = new List<MergePolicy>();
            if (_options.NoExtraCommitsMergePolicy)
            {
                mergePolicies.Add(new MergePolicy("NoExtraCommits", null));
            }
            if (_options.AllChecksSuccessfulMergePolicy)
            {
                mergePolicies.Add(new MergePolicy("AllChecksSuccessful", new Dictionary<string, object>
                {
                    { "ignoreChecks", _options.IgnoreChecks }
                }));
            }
            if (_options.RequireChecksMergePolicy.Count() > 0)
            {
                mergePolicies.Add(new MergePolicy("RequireChecks", new Dictionary<string, object>
                {
                    { "checks", _options.RequireChecksMergePolicy }
                }));
            }

            string channel = _options.Channel;
            string sourceRepository = _options.SourceRepository;
            string targetRepository = _options.TargetRepository;
            string targetBranch = _options.TargetBranch;
            string updateFrequency = _options.UpdateFrequency;

            // If in quiet (non-interactive mode), ensure that all options were passed, then
            // just call the remote API
            if (_options.Quiet)
            {
                if (string.IsNullOrEmpty(channel) ||
                    string.IsNullOrEmpty(sourceRepository) ||
                    string.IsNullOrEmpty(targetRepository) ||
                    string.IsNullOrEmpty(targetBranch) ||
                    string.IsNullOrEmpty(updateFrequency) ||
                    !Constants.AvailableFrequencies.Contains(updateFrequency, StringComparer.OrdinalIgnoreCase))
                {
                    Logger.LogError($"Missing input parameters for the subscription. Please see command help or remove --quiet/-q for interactive mode");
                    return Constants.ErrorCode;
                }
            }
            else
            {
                // Grab existing subscriptions to get suggested values.
                // TODO: When this becomes paged, set a max number of results to avoid
                // pulling too much.
                var suggestedRepos = remote.GetSubscriptionsAsync();
                var suggestedChannels = remote.GetChannelsAsync();

                // Help the user along with a form.  We'll use the API to gather suggested values
                // from existing subscriptions based on the input parameters.
                AddSubscriptionPopUp initEditorPopUp =
                    new AddSubscriptionPopUp("add-subscription/add-subscription-todo",
                                             Logger,
                                             channel,
                                             sourceRepository,
                                             targetRepository,
                                             targetBranch,
                                             updateFrequency,
                                             mergePolicies,
                                             (await suggestedChannels).Select(suggestedChannel => suggestedChannel.Name),
                                             (await suggestedRepos).SelectMany(subscription => new List<string> {subscription.SourceRepository, subscription.TargetRepository }).ToHashSet(),
                                             Constants.AvailableFrequencies,
                                             Constants.AvailableMergePolicyYamlHelp);

                UxManager uxManager = new UxManager(Logger);
                int exitCode = uxManager.PopUp(initEditorPopUp);
                if (exitCode != Constants.SuccessCode)
                {
                    return exitCode;
                }
                channel = initEditorPopUp.Channel;
                sourceRepository = initEditorPopUp.SourceRepository;
                targetRepository = initEditorPopUp.TargetRepository;
                targetBranch = initEditorPopUp.TargetBranch;
                updateFrequency = initEditorPopUp.UpdateFrequency;
                mergePolicies = initEditorPopUp.MergePolicies;
            }

            try
            {
                var newSubscription = await remote.CreateSubscriptionAsync(channel,
                                                                           sourceRepository,
                                                                           targetRepository,
                                                                           targetBranch,
                                                                           updateFrequency,
                                                                           mergePolicies);
                Console.WriteLine($"Successfully created new subscription with id '{newSubscription.Id}'.");
                return Constants.SuccessCode;
            }
            catch (ApiErrorException e) when (e.Response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                // Could have been some kind of validation error (e.g. channel doesn't exist)
                Logger.LogError($"Failed to create subscription: {e.Response.Content}");
                return Constants.ErrorCode;
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Failed to create subscription.");
                return Constants.ErrorCode;
            }
        }
    }
}
