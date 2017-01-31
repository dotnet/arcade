using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.ApiUsageAnalyzer.Core
{
    public struct AppAnalysis
    {
        public AppAnalysis(string name)
            : this(name, ImmutableList.Create<LibraryAnalysis>())
        {
        }

        public AppAnalysis(string name, IEnumerable<LibraryAnalysis> libraries)
            : this(name, libraries.ToImmutableList())
        {
        }

        private AppAnalysis(string name, IImmutableList<LibraryAnalysis> libraries)
        {
            Name = name;
            Libraries = libraries;
        }

        public string Name { get; }
        public IImmutableList<LibraryAnalysis> Libraries { get; }

        public AppAnalysis AddLibrary(LibraryAnalysis library)
        {
            return new AppAnalysis(Name, Libraries.Add(library));
        }
    }
}