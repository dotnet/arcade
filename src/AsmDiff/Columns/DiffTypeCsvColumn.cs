// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;
using Microsoft.Cci.Extensions;
using Microsoft.Cci.Mappings;

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public sealed class DiffTypeCsvColumn : DiffCsvColumn
    {
        public DiffTypeCsvColumn(DiffConfiguration diffConfiguration)
            : base(diffConfiguration)
        {
        }

        public override string Name
        {
            get { return "Type"; }
        }

        public override string GetValue(TypeMapping mapping)
        {
            var typeDefinition = mapping.Representative;
            return GetTypeName(typeDefinition);
        }

        public override string GetValue(MemberMapping mapping)
        {
            var typeDefinitionMember = mapping.Representative;
            var typeDefinition = typeDefinitionMember.ContainingTypeDefinition;
            return GetTypeName(typeDefinition);
        }

        private static string GetTypeName(ITypeDefinition typeDefinition)
        {
            return typeDefinition.GetTypeName(NameFormattingOptions.OmitContainingNamespace |
                                              NameFormattingOptions.TypeParameters);
        }
    }
}
