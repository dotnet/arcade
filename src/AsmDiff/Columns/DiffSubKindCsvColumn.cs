// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Extensions;
using Microsoft.Cci.Mappings;

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public sealed class DiffSubKindCsvColumn : DiffCsvColumn
    {
        public DiffSubKindCsvColumn(DiffConfiguration diffConfiguration)
            : base(diffConfiguration)
        {
        }

        public override string Name
        {
            get { return "Sub Kind"; }
        }

        public override string GetValue(NamespaceMapping mapping)
        {
            return ApiKind.Namespace.ToString();
        }

        public override string GetValue(TypeMapping mapping)
        {
            var typeDefinition = mapping.Representative;
            return typeDefinition.GetApiKind().ToString();
        }

        public override string GetValue(MemberMapping mapping)
        {
            var member = mapping.Representative;
            return member.GetApiKind().ToString();
        }
    }
}
