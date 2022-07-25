// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.Cci;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Filters;

namespace Microsoft.DotNet.AsmDiff
{
    internal sealed class DiffCciFilter : ICciFilter
    {
        private readonly string[] _skippableAttributes = new[]
        {
            "System.AttributeUsageAttribute",
            "System.ComponentModel.DefaultEventAttribute",
            "System.ComponentModel.DefaultPropertyAttribute",
            "System.ComponentModel.DesignerAttribute",
            "System.ComponentModel.DesignerCategoryAttribute",
            "System.ComponentModel.DesignerSerializationVisibilityAttribute",
            "System.ComponentModel.DesignTimeVisibleAttribute",
            "System.ComponentModel.EditorAttribute",
            "System.ComponentModel.EditorBrowsableAttribute",
            "System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute",
            "System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute",
            "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute",
            "System.Diagnostics.DebuggerDisplayAttribute",
            "System.Diagnostics.DebuggerHiddenAttribute",
            "System.Diagnostics.DebuggerStepThroughAttribute",
            "System.Runtime.CompilerServices.AsyncStateMachineAttribute",
            "System.Runtime.CompilerServices.CompilerFeatureRequiredAttribute",
            "System.Runtime.CompilerServices.CompilerGeneratedAttribute",
            "System.Runtie.CompilerServices.InterpolatedStringHandlerAttribute",
            "System.Runtime.CompilerServices.NullableAttribute",
            "System.Runtime.CompilerServices.NullableContextAttribute",
            "System.Runtime.InteropServices.StructLayoutAttribute",
        };

        public DiffCciFilter(bool includeAttributes = false, bool includeInternals = false, bool includePrivates = false, bool includeGenerated = false)
        {
            IncludeAttributes = includeAttributes;
            IncludeInternals = includeInternals;
            IncludePrivates = includePrivates;
            IncludeGenerated = includeGenerated;
        }

        public bool Include(ICustomAttribute attribute)
        {
            // If we can't resolve an attribute, we err on the side of including it.
            // This makes sure we don't accidentally hide all the attributes just
            // because we can't resolve the core assembly.
            if (!IncludeAttributes)
                return false;

            if (attribute.Type == null)
                return false;

            var resolvedType = attribute.Type.ResolvedType;
            string name = resolvedType.ToString();
            bool isNull = name != null;
            bool contains = _skippableAttributes.Contains(name);
            bool dummy = resolvedType is Dummy;
            bool include = Include(resolvedType);
            bool result = isNull && !contains && (dummy || include);
            return result;
        }

        public bool Include(INamespaceDefinition ns)
        {
            return ns.GetTypes(IncludeForwardedTypes).Any(Include);
        }

        public bool Include(ITypeDefinition type)
        {
            if (type == null || type is Dummy)
                return false;

            if (!IncludeGenerated && IsCompilerGenerated(type))
                return false;

            switch (type.GetVisibility())
            {
                case TypeMemberVisibility.Private:
                    return IncludePrivates;

                case TypeMemberVisibility.Assembly:
                case TypeMemberVisibility.FamilyAndAssembly:
                    return IncludeInternals;

                case TypeMemberVisibility.Family:
                case TypeMemberVisibility.FamilyOrAssembly:
                    return true;

                case TypeMemberVisibility.Public:
                    return true;
            }

            return type.IsVisibleOutsideAssembly();
        }

        public bool Include(ITypeDefinitionMember member)
        {
            if (member == null || member is Dummy)
                return false;

            if (!Include(member.ContainingTypeDefinition))
                return false;

            if (!IncludeGenerated && IsCompilerGenerated(member))
            {
                // If it's an accessor, we want to fall-through.

                var propertyAccessors = from p in member.ContainingTypeDefinition.Properties
                                        from a in p.Accessors
                                        select a;

                var eventAccessors = from e in member.ContainingTypeDefinition.Events
                                     from a in e.Accessors
                                     select a;

                var accessors = propertyAccessors.Concat(eventAccessors);

                var isAccessor = (from a in accessors
                                  where a.ResolvedMethod == member
                                  select a).Any();

                if (!isAccessor)
                    return false;
            }

            switch (member.Visibility)
            {
                case TypeMemberVisibility.Private:
                    return IncludePrivates  || MemberHelper.IsVisibleOutsideAssembly(member);;

                case TypeMemberVisibility.Assembly:
                case TypeMemberVisibility.FamilyAndAssembly:
                    return IncludeInternals;

                case TypeMemberVisibility.Family:
                case TypeMemberVisibility.FamilyOrAssembly:
                    return true;

                case TypeMemberVisibility.Public:
                    return true;
            }

            return member.IsVisibleOutsideAssembly();
        }

        private static bool IsCompilerGenerated(ITypeDefinition typeDefinition)
        {
            var generatedAttribute = typeDefinition.PlatformType.SystemRuntimeCompilerServicesCompilerGeneratedAttribute;
            return typeDefinition.Attributes.Where(a => TypeHelper.TypesAreEquivalent(a.Type, generatedAttribute)).Any();
        }

        private static bool IsCompilerGenerated(ITypeDefinitionMember typeDefinitionMember)
        {
            var generatedAttribute = typeDefinitionMember.ContainingTypeDefinition.PlatformType.SystemRuntimeCompilerServicesCompilerGeneratedAttribute;
            return typeDefinitionMember.Attributes.Where(a => TypeHelper.TypesAreEquivalent(a.Type, generatedAttribute)).Any();
        }

        public bool IncludeAttributes { get; set; }

        public bool IncludeInternals { get; set; }

        public bool IncludePrivates { get; set; }

        public bool IncludeGenerated { get; set; }

        public bool IncludeForwardedTypes { get; set; }
    }
}
