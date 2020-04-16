// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Extensions.CSharp;
using Microsoft.Cci.Mappings;

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public sealed class DiffOverrideCsvColumn : DiffCsvColumn
    {
        public DiffOverrideCsvColumn(DiffConfiguration diffConfiguration)
            : base(diffConfiguration)
        {
        }

        public override string Name
        {
            get { return "Override"; }
        }

        public override string GetValue(NamespaceMapping mapping)
        {
            return string.Empty;
        }

        public override string GetValue(TypeMapping mapping)
        {
            return string.Empty;
        }

        public override string GetValue(MemberMapping mapping)
        {
            var isOverride = mapping.Representative.IsOverride();
            return isOverride
                       ? "Yes"
                       : "No";
        }
    }
}
