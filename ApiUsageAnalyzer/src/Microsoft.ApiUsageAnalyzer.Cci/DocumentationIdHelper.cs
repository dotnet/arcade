using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Cci;

namespace Microsoft.ApiUsageAnalyzer.Cci
{
    internal static class DocumentationIdHelper
    {
        public static string GetId(object obj)
        {
            switch (obj)
            {
                case string namespaceName:
                    return GetId(namespaceName);
                case IUnitNamespaceReference ns:
                    return GetId(TypeHelper.GetNamespaceName(ns, NameFormattingOptions.None));
                case ITypeReference typeReference:
                    return GetId(typeReference);
                case ITypeMemberReference typeMemberReference:
                    return GetId(typeMemberReference);
                default:
                    throw new ArgumentException(obj?.GetType().Name ?? "null");
            }
        }

        public static string GetId(string namespaceName)
        {
            return "N:" + namespaceName;
        }

        public static string GetId(ITypeReference typeReference)
        {
            return TypeHelper.GetTypeName(typeReference, NameFormattingOptions.DocumentationId);
        }

        public static string GetId(ITypeMemberReference typeMemberReference)
        {
            return MemberHelper.GetMemberSignature(typeMemberReference, NameFormattingOptions.DocumentationId);
        }
    }
}
