// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Maestro.MergePolicies;

namespace SubscriptionActorService
{
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
}
