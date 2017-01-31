using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Microsoft.ApiUsageAnalyzer.Core
{
    public interface IApiUsageCollector
    {
        IImmutableList<(string api, int count)> GetApiUsage(string path);
    }
}
