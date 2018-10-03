using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
    public class MergePolicyEvaluationContext : IMergePolicyEvaluationContext
    {
        public MergePolicyEvaluationContext(
            string pullRequestUrl,
            IRemote darc)
        {
            PullRequestUrl = pullRequestUrl;
            Darc = darc;
        }

        public string PullRequestUrl { get; }
        public IRemote Darc { get; }

        public MergePolicyEvaluationResult Result => new MergePolicyEvaluationResult(PolicyResults);

        public void Pending(string message)
        {
            PolicyResults.Add(new MergePolicyEvaluationResult.SingleResult(null, message, CurrentPolicy));
        }

        public void Succeed(string message)
        {
            PolicyResults.Add(new MergePolicyEvaluationResult.SingleResult(true, message, CurrentPolicy));
        }

        public void Fail(string message)
        {
            PolicyResults.Add(new MergePolicyEvaluationResult.SingleResult(false, message, CurrentPolicy));
        }

        private IList<MergePolicyEvaluationResult.SingleResult> PolicyResults { get; } = new List<MergePolicyEvaluationResult.SingleResult>();

        internal MergePolicy CurrentPolicy { get; set; }
    }

    public class MergePolicyEvaluationResult
    {
        public MergePolicyEvaluationResult(IEnumerable<SingleResult> results)
        {
            Results = results.ToImmutableList();
        }

        public IReadOnlyList<SingleResult> Results { get; }

        public bool Succeeded => Results.Count > 0 && Results.All(r => r.Success == true);

        public bool Pending => Results.Count > 0 && Results.Any(r => r.Success == null);

        public bool Failed => Results.Count > 0 && Results.Any(r => r.Success == false);

        public class SingleResult
        {
            public SingleResult(bool? success, string message, MergePolicy policy)
            {
                Success = success;
                Message = message;
                Policy = policy;
            }

            public bool? Success { get; }
            public string Message { get; }
            public MergePolicy Policy { get; }
        }
    }

    public class MergePolicyEvaluator
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
