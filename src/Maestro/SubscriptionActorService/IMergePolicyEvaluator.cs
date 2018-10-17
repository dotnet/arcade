using System.Collections.Generic;
using System.Threading.Tasks;
using Maestro.Data.Models;
using Microsoft.DotNet.DarcLib;

namespace SubscriptionActorService
{
    public interface IMergePolicyEvaluator
    {
        Task<MergePolicyEvaluationResult> EvaluateAsync(string prUrl, IRemote darc, IReadOnlyList<MergePolicyDefinition> policyDefinitions);
    }
}