// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.DarcLib;

namespace Maestro.MergePolicies
{
    /// <summary>
    ///   Merge the PR when it has more than one check and they are all successful, ignoring checks specified in the "ignoreChecks" property.
    /// </summary>
    public class AllChecksSuccessfulMergePolicy : MergePolicy
    {
        public override string DisplayName => "All Checks Successful";

        protected override async Task<MergePolicyEvaluationResult> DoEvaluateAsync(MergePolicyEvaluationContext context)
        {
            var ignoreChecks = new HashSet<string>(context.Get<string[]>("ignoreChecks") ?? Array.Empty<string>());

            IList<Check> checks = await context.Darc.GetPullRequestChecksAsync(context.PullRequestUrl);

            List<Check> notIgnoredChecks = checks.Where(c => !ignoreChecks.Contains(c.Name)).ToList();

            if (notIgnoredChecks.Count < 1)
            {
                return context.Fail("No unignored checks.");
            }

            List<Check> failedChecks = notIgnoredChecks.Where(c => c.Status != CheckState.Success).ToList();

            if (failedChecks.Count < 1)
            {
                return context.Success();
            }

            return context.Fail($"Unsuccessful checks: {string.Join(", ", failedChecks.Select(c => c.Name))}");
        }
    }
}
