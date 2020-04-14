// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Extensions;
using Microsoft.Cci.Mappings;

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public sealed class DiffIdCsvColumn : DiffCsvColumn
    {
        public DiffIdCsvColumn(DiffConfiguration diffConfiguration)
            : base(diffConfiguration)
        {
        }

        public override string Name
        {
            get { return "ID"; }
        }

        public override string GetValue(NamespaceMapping mapping)
        {
            var namespaceDefinition = mapping.Representative;
            return namespaceDefinition.DocId();
        }

        public override string GetValue(TypeMapping mapping)
        {
            var typeDefinition = mapping.Representative;
            return typeDefinition.DocId();
        }

        public override string GetValue(MemberMapping mapping)
        {
            var typeDefinitionMember = mapping.Representative;
            return typeDefinitionMember.DocId();
        }
    }
}
