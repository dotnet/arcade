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

            // Include based on member visibility (simple cases).
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

            // Include explicit interface implementations.
            if (member.IsVisibleToFriendAssemblies())
            {
                return true;
            }

            // If a type is abstract and has an internal or public constructor, it must expose all abstract members.
            if (containingType.IsAbstract &&
                member.IsAbstract() &&
                containingType.IsConstructorVisibleToFriendAssemblies())
            {
                return true;
            }

            // Otherwise...
            return false;
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
    }
}
