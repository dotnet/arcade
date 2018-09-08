using System.Linq;
using System.Threading.Tasks;

namespace Maestro.MergePolicies
{
    public class NoExtraCommitsMergePolicy : MergePolicy
    {
        public override string DisplayName => "No Extra Commits";

        protected override Task<MergePolicyEvaluationResult> DoEvaluateAsync(MergePolicyEvaluationContext context)
        {
            return Task.FromResult(context.Fail("Merge Policy Not Yet Implemented."));
        }
    }
}
