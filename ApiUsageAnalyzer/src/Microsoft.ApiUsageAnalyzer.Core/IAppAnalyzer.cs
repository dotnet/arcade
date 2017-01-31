using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ApiUsageAnalyzer.Core
{
    public interface IAppAnalyzer
    {
        bool TryAnalyzeApp(string path, out AppAnalysis analysis);
    }
}
