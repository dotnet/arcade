// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.DarcLib;

namespace Maestro.MergePolicies
{
    /// <summary>
    ///     Merge the PR when it has all the checks specified in the "checks" property and they are all successful.
    /// </summary>
    public class RequireSuccessfulChecksMergePolicy : MergePolicy
    {
        public override string DisplayName => "Require Successful Checks";

        public override async Task EvaluateAsync(
            IMergePolicyEvaluationContext context,
            MergePolicyProperties properties)
        {
            var requiredChecks = new HashSet<string>(properties.Get<List<string>>("checks"));

            Dictionary<string, Check> checks =
                (await context.Darc.GetPullRequestChecksAsync(context.PullRequestUrl)).ToDictionary(c => c.Name);

            var missingChecks = new List<string>();
            var failedChecks = new List<Check>();

            foreach (string requiredCheck in requiredChecks)
            {
                if (checks.TryGetValue(requiredCheck, out Check check))
                {
                    if (check.Status != CheckState.Success)
                    {
                        failedChecks.Add(check);
                    }
                }
                else
                {
                    missingChecks.Add(requiredCheck);
                }
            }

            if (missingChecks.Count < 1 && failedChecks.Count < 1)
            {
                context.Succeed("Required checks passed.");
                return;
            }

            var parts = new List<string>();

            if (failedChecks.Any())
            {
                parts.Add($"Unsuccessful checks: {string.Join(", ", failedChecks.Select(c => c.Name))}");
            }

            if (missingChecks.Any())
            {
                parts.Add($"Missing checks: {string.Join(", ", missingChecks)}");
            }

            context.Fail(string.Join("; ", parts));
        }
    }
}
