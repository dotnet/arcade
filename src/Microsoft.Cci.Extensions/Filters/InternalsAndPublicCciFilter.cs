// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Extensions.CSharp;

namespace Microsoft.Cci.Filters
{
    /// <summary>
    /// An <see cref="ICciFilter"/> to include <c>internal</c> and <c>public</c> members.
    /// </summary>
    /// <remarks>
    /// This is a variant of <see cref="PublicOnlyCciFilter"/>. This <see cref="ICciFilter"/> has the following
    /// differences:
    /// <list type="number">
    /// <item>Includes <c>internal</c> members.</item>
    /// <item>Adds more parameter <see langword="null"/> checks.</item>
    /// <item>Reorders a few checks.</item>
    /// </list>
    /// </remarks>
    public class InternalsAndPublicCciFilter : ICciFilter
    {
        public InternalsAndPublicCciFilter(bool excludeAttributes = true)
            : this(excludeAttributes, includeForwardedTypes: false)
        {
        }

        public InternalsAndPublicCciFilter(bool excludeAttributes, bool includeForwardedTypes)
        {
            ExcludeAttributes = excludeAttributes;
            IncludeForwardedTypes = includeForwardedTypes;
        }

        public bool IncludeForwardedTypes { get; set; }

        public bool ExcludeAttributes { get; set; }

        public virtual bool Include(INamespaceDefinition ns)
        {
            if (ns == null)
            {
                return false;
            }

            // Only include non-empty namespaces.
            return ns.GetTypes(IncludeForwardedTypes).Any(Include);
        }

        public virtual bool Include(ITypeDefinition type)
        {
            if (type == null || Dummy.Type == type)
            {
                return false;
            }

            return TypeHelper.IsVisibleToFriendAssemblies(type);
        }

        public virtual bool Include(ITypeDefinitionMember member)
        {
            var containingType = member?.ContainingTypeDefinition;
            if (!Include(containingType))
            {
                return false;
            }

            // Handle simple cases. (At this point, both member and containingType must be non-null.)
            if (SimpleInclude(member))
            {
                return true;
            }

            // If a type is abstract and has an internal or public constructor, it must expose all abstract members.
            if (containingType.IsAbstract && member.IsAbstract())
            {
                foreach (var method in containingType.Methods)
                {
                    if (method.IsConstructor && method.Visibility != TypeMemberVisibility.Private)
                    {
                        return true;
                    }
                }
            }

            // Include explicit interface implementations. (Inspired by MemberHelper.IsVisibleOutsideAssembly(...).)
            return member switch
            {
                IMethodDefinition methodDefinition =>
                    IsExplicitImplementationVisible(methodDefinition, containingType),
                IPropertyDefinition propertyDefinition =>
                    IsExplicitImplementationVisible(propertyDefinition.Getter, containingType) ||
                        IsExplicitImplementationVisible(propertyDefinition.Setter, containingType),
                IEventDefinition eventDefinition =>
                    IsExplicitImplementationVisible(eventDefinition.Adder, containingType) ||
                        IsExplicitImplementationVisible(eventDefinition.Remover, containingType),
                // Otherwise...
                _ => false,
            };
        }

        public virtual bool Include(ICustomAttribute attribute)
        {
            if (attribute == null || ExcludeAttributes)
            {
                return false;
            }

            // Exclude attributes not visible outside the assembly.
            var attributeDef = attribute.Type.GetDefinitionOrNull();
            if (attributeDef != null && !TypeHelper.IsVisibleToFriendAssemblies(attributeDef))
            {
                return false;
            }

            // Exclude attributes with typeof() argument of a type invisible to friend assemblies.
            foreach (var arg in attribute.Arguments.OfType<IMetadataTypeOf>())
            {
                var typeDef = arg.TypeToGet.GetDefinitionOrNull();
                if (typeDef != null && !TypeHelper.IsVisibleToFriendAssemblies(typeDef))
                {
                    return false;
                }
            }

            // Otherwise...
            return true;
        }

        private bool SimpleInclude(ITypeDefinitionMember member)
        {
            switch (member.Visibility)
            {
                case TypeMemberVisibility.Assembly:
                case TypeMemberVisibility.FamilyOrAssembly:
                case TypeMemberVisibility.Public:
                    return true;

                case TypeMemberVisibility.Family:
                case TypeMemberVisibility.FamilyAndAssembly:
                    // Similar to special case in PublicOnlyCciFilter, include protected members even of a sealed type.
                    // This is necessary to generate compilable code e.g. a derived type in current assembly may
                    // override the protected member.
                    return true;
            }

            return false;
        }

        // A rewrite of MemberHelper.IsExplicitImplementationVisible(...) with looser visibility checks.
        private bool IsExplicitImplementationVisible(IMethodReference method, ITypeDefinition containingType)
        {
            if (method == null)
            {
                return false;
            }

            using var enumerator = containingType.ExplicitImplementationOverrides.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
                if (current.ImplementingMethod.InternedKey == method.InternedKey)
                {
                    var resolvedMethod = current.ImplementedMethod.ResolvedMethod;
                    if (resolvedMethod is Dummy)
                    {
                        return true;
                    }

                    if (SimpleInclude(resolvedMethod))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
