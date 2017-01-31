using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.ApiUsageAnalyzer.Core
{
    public struct LibraryAnalysis
    {
        public LibraryAnalysis(string name, string tfm)
            : this(name, tfm, ImmutableDictionary.Create<string, int>())
        {
        }

        public LibraryAnalysis(string name, string tfm, IEnumerable<(string name, int count)> usedApis)
            : this(name, tfm, usedApis.ToImmutableDictionary(p => p.name, p => p.count))
        {
        }

        public LibraryAnalysis(string name, string tfm, IImmutableDictionary<string, int> usedApis)
        {
            Name = name;
            TFM = tfm;
            UsedApis = usedApis;
        }

        public string Name { get; }
        public string TFM { get; }
        public IImmutableDictionary<string, int> UsedApis { get; }

        public LibraryAnalysis AddUsedApi(string api, int count)
        {
            return new LibraryAnalysis(Name, TFM, UsedApis.Add(api, count));
        }
    }
}