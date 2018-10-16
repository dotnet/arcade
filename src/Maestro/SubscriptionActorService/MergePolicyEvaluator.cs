// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Maestro.Data.Models;
using Maestro.MergePolicies;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;

namespace SubscriptionActorService
{
    public class MergePolicyEvaluator : IMergePolicyEvaluator
    {
        public MergePolicyEvaluator(IEnumerable<MergePolicy> mergePolicies, ILogger<MergePolicyEvaluator> logger)
        {
            MergePolicies = mergePolicies.ToImmutableDictionary(p => p.Name);
            Logger = logger;
        }

        public IImmutableDictionary<string, MergePolicy> MergePolicies { get; }
        public ILogger<MergePolicyEvaluator> Logger { get; }

        public async Task<MergePolicyEvaluationResult> EvaluateAsync(
            string prUrl,
            IRemote darc,
            IReadOnlyList<MergePolicyDefinition> policyDefinitions)
        {
            var context = new MergePolicyEvaluationContext(prUrl, darc);
            foreach (MergePolicyDefinition definition in policyDefinitions)
            {
                if (MergePolicies.TryGetValue(definition.Name, out MergePolicy policy))
                {
                    using (Logger.BeginScope("Evaluating Merge Policy {policyName}", policy.Name))
                    {
                        context.CurrentPolicy = policy;
                        await policy.EvaluateAsync(context, new MergePolicyProperties(definition.Properties));
                    }
                }
                else
                {
                    context.CurrentPolicy = null;
                    context.Fail($"Unknown Merge Policy: '{definition.Name}'");
                }
            }

            return context.Result;
        }
    }
}
