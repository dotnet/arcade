// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Maestro.MergePolicies;
using Microsoft.DotNet.DarcLib;

namespace SubscriptionActorService
{
    public class MergePolicyEvaluationContext : IMergePolicyEvaluationContext
    {
        public MergePolicyEvaluationContext(string pullRequestUrl, IRemote darc)
        {
            PullRequestUrl = pullRequestUrl;
            Darc = darc;
        }

        public MergePolicyEvaluationResult Result => new MergePolicyEvaluationResult(PolicyResults);

        private IList<MergePolicyEvaluationResult.SingleResult> PolicyResults { get; } =
            new List<MergePolicyEvaluationResult.SingleResult>();

        internal MergePolicy CurrentPolicy { get; set; }

        public string PullRequestUrl { get; }
        public IRemote Darc { get; }

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
    }
}
