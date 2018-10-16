using Microsoft.DotNet.DarcLib;

namespace Maestro.MergePolicies
{
    public interface IMergePolicyEvaluationContext
    {
        IRemote Darc { get; }
        string PullRequestUrl { get; }
        void Succeed(string message);
        void Fail(string message);
        void Pending(string message);
    }
}
