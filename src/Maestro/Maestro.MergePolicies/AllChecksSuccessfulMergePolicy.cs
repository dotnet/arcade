using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.DarcLib;

namespace Maestro.MergePolicies
{
    public class AllChecksSuccessfulMergePolicy : MergePolicy
    {
        public override string DisplayName => "All Checks Successful";

        protected override async Task<MergePolicyEvaluationResult> DoEvaluateAsync(MergePolicyEvaluationContext context)
        {
            var ignoreChecks = new HashSet<string>(context.Get<string[]>("ignoreChecks") ?? Array.Empty<string>());

            var checks = await context.Darc.GetPullRequestChecksAsync(context.PullRequestUrl);

            var notIgnoredChecks = checks.Where(c => !ignoreChecks.Contains(c.Name)).ToList();

            if (notIgnoredChecks.Count < 1)
            {
                return context.Fail("No unignored checks.");
            }

            var failedChecks = notIgnoredChecks.Where(c => c.Status != CheckState.Success).ToList();

            if (failedChecks.Count < 1)
            {
                return context.Success();
            }

            return context.Fail($"Unsuccessful checks: {string.Join(", ", failedChecks.Select(c => c.Name))}");
        }
    }
}
