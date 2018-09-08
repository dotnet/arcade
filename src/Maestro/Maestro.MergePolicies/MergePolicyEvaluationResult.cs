namespace Maestro.MergePolicies
{
    public class MergePolicyEvaluationResult
    {
        internal MergePolicyEvaluationResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message;
        }

        public bool Succeeded { get; }
        public string Message { get; }
    }
}