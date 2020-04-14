// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Cci;

namespace Microsoft.Fx.ApiReviews.Differencing.Helpers
{
    internal static class NameHelper
    {
        public static string GetName(object obj)
        {
            var assembly = obj as IAssembly;
            if (assembly != null)
                return GetName(assembly);

            var namespaceDefinition = obj as INamespaceDefinition;
            if (namespaceDefinition != null)
                return GetName(namespaceDefinition);

            var typeMemberReference = obj as ITypeMemberReference;
            if (typeMemberReference != null)
                return GetName(typeMemberReference);

            var typeReference = obj as ITypeReference;
            if (typeReference != null)
                return GetName(typeReference);

            throw new NotImplementedException("Unknown CCI object type: " + obj.GetType());
        }

        private static string GetName(IAssembly assembly)
        {
            return assembly.Name.Value;
        }

        private static string GetName(INamespaceDefinition namespaceName)
        {
            var name = namespaceName.ToString();
            return string.IsNullOrEmpty(name)
                       ? "-"
                       : name;
        }

        private static string GetName(ITypeReference typeReference)
        {
            return TypeHelper.GetTypeName(typeReference, NameFormattingOptions.TypeParameters |
                                                         NameFormattingOptions.OmitContainingNamespace);
        }

        private static string GetName(ITypeMemberReference typeMemberReference)
        {
            var memberSignature = MemberHelper.GetMemberSignature(typeMemberReference, NameFormattingOptions.Signature |
                                                                                       NameFormattingOptions.OmitContainingType |
                                                                                       NameFormattingOptions.OmitContainingNamespace |
                                                                                       NameFormattingOptions.PreserveSpecialNames);

            var returnTypeName = GetReturnTypeName(typeMemberReference);
            return returnTypeName == null
                       ? memberSignature
                       : memberSignature + " : " + returnTypeName;
        }

        private static string GetReturnTypeName(ITypeMemberReference typeMemberReference)
        {
            var typeDefinitionMember = typeMemberReference.ResolvedTypeDefinitionMember;
            var fieldDefinition = typeDefinitionMember as IFieldDefinition;
            if (fieldDefinition != null)
                return GetName(fieldDefinition.Type);

            var propertyDefinition = typeDefinitionMember as IPropertyDefinition;
            if (propertyDefinition != null)
                return GetName(propertyDefinition.Type);

            var methodDefinition = typeDefinitionMember as IMethodDefinition;
            if (methodDefinition != null && !methodDefinition.IsConstructor && !methodDefinition.IsStaticConstructor)
                return GetName(methodDefinition.Type);

            return null;
        }
    }
}
