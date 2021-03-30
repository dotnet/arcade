using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility
{
    public class ApiDiffer
    {
        private readonly DiffingSettings _settings;

        public ApiDiffer() : this(new DiffingSettings()) { }

        public ApiDiffer(DiffingSettings settings)
        {
            _settings = settings;
        }

        public ApiDiffer(bool includeInternalSymbols)
        {
            _settings = new DiffingSettings(filter: new AccessibilityFilter(includeInternalSymbols));
        }

        public string NoWarn { get; set; } = string.Empty;
        public (string diagnosticId, string memberId)[] IgnoredDifferences { get; set; }

        public IEnumerable<CompatDifference> GetDifferences(IEnumerable<IAssemblySymbol> left, IEnumerable<IAssemblySymbol> right)
        {
            AssemblySetMapper mapper = new AssemblySetMapper(_settings);
            mapper.AddElement(left, 0);
            mapper.AddElement(right, 1);

            DiferenceVisitor visitor = new DiferenceVisitor(noWarn: NoWarn, ignoredDifferences: IgnoredDifferences);
            visitor.Visit(mapper);
            return visitor.Differences;
        }

        public IEnumerable<CompatDifference> GetDifferences(IEnumerable<INamespaceSymbol> left, IEnumerable<IAssemblySymbol> right)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<CompatDifference> GetDifferences(IEnumerable<IAssemblySymbol> left, IEnumerable<INamespaceSymbol> right)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<CompatDifference> GetDifferences(IEnumerable<INamespaceSymbol> left, IEnumerable<INamespaceSymbol> right)
        {
            throw new NotImplementedException();
        }
    }
}
