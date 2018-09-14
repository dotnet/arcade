// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Maestro.MergePolicies
{
    public abstract class MergePolicy
    {
        public string Name
        {
            get
            {
                string name = GetType().Name;
                if (name.EndsWith(nameof(MergePolicy)))
                {
                    name = name.Substring(0, name.Length - nameof(MergePolicy).Length);
                }

                return name;
            }
        }

        public abstract string DisplayName { get; }

        public async Task<MergePolicyEvaluationResult> EvaluateAsync(MergePolicyEvaluationContext context)
        {
            using (context.Logger.BeginScope("Evaluating Merge Policy {policyName}", Name))
            {
                return await DoEvaluateAsync(context);
            }
        }

        protected abstract Task<MergePolicyEvaluationResult> DoEvaluateAsync(MergePolicyEvaluationContext context);
    }
}
