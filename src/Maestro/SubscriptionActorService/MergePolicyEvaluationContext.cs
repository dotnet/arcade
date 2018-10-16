using System.Collections.Generic;
using Maestro.MergePolicies;
using Microsoft.DotNet.DarcLib;
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
}