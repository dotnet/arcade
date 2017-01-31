using System.Collections.Generic;

namespace Microsoft.ApiUsageAnalyzer.Core
{
    public interface IOutputFormat
    {
        string Id { get; }
        string Description { get; }
        void FormatOutputFile(string destinationPath, IReadOnlyList<AppAnalysis> analyses);
        void FormatOutputDirectory(string destinationPath, IReadOnlyList<AppAnalysis> analyses);
    }
}