using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Maestro.Data.Models;
using Maestro.MergePolicies;
using Microsoft.DotNet.DarcLib;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MergePolicy = Maestro.MergePolicies.MergePolicy;

namespace SubscriptionActorService
{
    public class MergePolicyEvaluator : IMergePolicyEvaluator
    {
        public IImmutableDictionary<string, MergePolicy> MergePolicies { get; }
        public ILogger<MergePolicyEvaluator> Logger { get; }

        public MergePolicyEvaluator(IEnumerable<MergePolicy> mergePolicies, ILogger<MergePolicyEvaluator> logger)
        {
            MergePolicies = mergePolicies.ToImmutableDictionary(p => p.Name);
            Logger = logger;
        }

        public async Task<MergePolicyEvaluationResult> EvaluateAsync(string prUrl, IRemote darc, IReadOnlyList<MergePolicyDefinition> policyDefinitions)
        {
            var context = new MergePolicyEvaluationContext(prUrl, darc);
            foreach (var definition in policyDefinitions)
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
