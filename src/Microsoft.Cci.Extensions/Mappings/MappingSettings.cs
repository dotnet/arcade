// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Cci.Comparers;
using Microsoft.Cci.Differs;
using Microsoft.Cci.Filters;

namespace Microsoft.Cci.Mappings
{
    public class MappingSettings
    {
        public MappingSettings(bool excludeAttributes = true)
        {
            ElementCount = 2;
            Filter = new PublicOnlyCciFilter(excludeAttributes);
            Comparers = CciComparers.Default;
            DiffFactory = new ElementDifferenceFactory();
        }

        public IEnumerable<string> AttributesToExclude { get; set; }

        public ICciFilter Filter { get; set; }

        public IMappingDifferenceFilter DiffFilter { get; set; }

        public ICciComparers Comparers { get; set; }

        public IElementDifferenceFactory DiffFactory { get; set; }

        public int ElementCount { get; set; }

        public bool FlattenTypeMembers { get; set; }

        public bool GroupByAssembly { get; set; }

        public bool IncludeForwardedTypes { get; set; }

        public bool AlwaysDiffMembers { get; set; }

        public IEqualityComparer<IAssembly> AssemblyComparer { get { return this.Comparers.GetEqualityComparer<IAssembly>(); } }

        public IEqualityComparer<INamespaceDefinition> NamespaceComparer { get { return this.Comparers.GetEqualityComparer<INamespaceDefinition>(); } }

        public IEqualityComparer<ITypeReference> TypeComparer { get { return this.Comparers.GetEqualityComparer<ITypeReference>(); } }

        public IEqualityComparer<ITypeDefinitionMember> MemberComparer { get { return this.Comparers.GetEqualityComparer<ITypeDefinitionMember>(); } }

        public IEqualityComparer<ICustomAttribute> AttributeComparer { get { return this.Comparers.GetEqualityComparer<ICustomAttribute>(); } }
    }
}
