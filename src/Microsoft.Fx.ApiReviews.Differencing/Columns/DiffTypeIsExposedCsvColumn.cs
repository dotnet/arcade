// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Extensions;
using Microsoft.Cci.Mappings;

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public sealed class DiffTypeIsExposedCsvColumn : DiffCsvColumn
    {
        public DiffTypeIsExposedCsvColumn(DiffConfiguration diffConfiguration)
            : base(diffConfiguration)
        {
        }

        public override string Name
        {
            get { return "TypeIsExposed"; }
        }

        public override string GetValue(TypeMapping mapping)
        {
            return mapping.Representative.IsVisibleOutsideAssembly()
                       ? "Yes"
                       : "No";
        }

        public override string GetValue(MemberMapping mapping)
        {
            return mapping.Representative.ContainingTypeDefinition.IsVisibleOutsideAssembly()
                       ? "Yes"
                       : "No";
        }
    }
}
