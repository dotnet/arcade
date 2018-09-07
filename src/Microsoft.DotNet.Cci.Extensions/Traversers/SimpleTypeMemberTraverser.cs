// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Filters;

namespace Microsoft.Cci.Traversers
{
    public class SimpleTypeMemberTraverser
    {
        private readonly ICciFilter _filter;

        public SimpleTypeMemberTraverser(ICciFilter filter)
        {
            _filter = filter ?? new IncludeAllFilter();
        }

        public ICciFilter Filter { get { return _filter; } }

        public bool IncludeForwardedTypes { get; set; }

        public virtual void Visit(IEnumerable<IAssembly> assemblies)
        {
            foreach (var assembly in assemblies)
                Visit(assembly);
        }

        public virtual void Visit(IAssembly assembly)
        {
            Visit(assembly.GetAllNamespaces());
        }

        public virtual void Visit(IEnumerable<INamespaceDefinition> namespaces)
        {
            namespaces = namespaces.Where(_filter.Include);
            namespaces = namespaces.Where(ns => ns.GetTypes(this.IncludeForwardedTypes).Any(_filter.Include));
            namespaces = namespaces.OrderBy(GetNamespaceKey, StringComparer.OrdinalIgnoreCase);

            foreach (var ns in namespaces)
                Visit(ns);
        }

        public virtual string GetNamespaceKey(INamespaceDefinition ns)
        {
            return ns.UniqueId();
        }

        public virtual void Visit(INamespaceDefinition ns)
        {
            Visit(ns.GetTypes(this.IncludeForwardedTypes));
        }

        public virtual void Visit(IEnumerable<ITypeDefinition> types)
        {
            types = types.Where(_filter.Include);
            types = types.OrderBy(GetTypeKey, StringComparer.OrdinalIgnoreCase);

            foreach (var type in types)
                Visit(type);
        }

        public virtual string GetTypeKey(ITypeDefinition type)
        {
            return type.UniqueId();
        }

        public virtual void Visit(ITypeDefinition type)
        {
            Visit(type, type.Fields);
            Visit(type.Methods.Where(m => m.IsConstructor));
            Visit(type.Properties);
            Visit(type.Events);
            Visit(type.Methods.Where(m => !m.IsConstructor));
            Visit((IEnumerable<ITypeDefinition>)type.NestedTypes);
        }

        public virtual void Visit(IEnumerable<ITypeDefinitionMember> members)
        {
            members = members.Where(_filter.Include);
            members = members.OrderBy(GetMemberKey, StringComparer.OrdinalIgnoreCase);

            foreach (var member in members)
                Visit(member);
        }

        public virtual void Visit(ITypeDefinition parentType, IEnumerable<IFieldDefinition> fields)
        {
            this.Visit((IEnumerable<ITypeDefinitionMember>)fields);
        }

        public virtual string GetMemberKey(ITypeDefinitionMember member)
        {
            return member.UniqueId();
        }

        public virtual void Visit(ITypeDefinitionMember member)
        {
        }
    }
}
