// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Maestro.MergePolicies
{
    /// <summary>
    ///     Merge the PR when it only has commits created by Maestro. This is not yet implemented.
    /// </summary>
    public class NoExtraCommitsMergePolicy : MergePolicy
    {
        public override string DisplayName => "No Extra Commits";

        public override Task EvaluateAsync(IMergePolicyEvaluationContext context, MergePolicyProperties properties)
        {
            context.Fail("Merge Policy Not Yet Implemented.");
            return Task.CompletedTask;
        }
    }
}
