namespace Microsoft.ApiUsageAnalyzer.Core
{
    public interface IInputFormat
    {
        string Id { get; }
        string Description { get; }
        bool CanProcess(string input);
        IAppAnalyzer CreateAnalyzer();
    }
}