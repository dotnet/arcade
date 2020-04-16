// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Cci.Differs;
using Microsoft.Cci.Mappings;

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public sealed class DiffInOldCsvColumn : DiffCsvColumn
    {
        public DiffInOldCsvColumn(DiffConfiguration diffConfiguration)
            : base(diffConfiguration)
        {
        }

        public override bool IsVisible
        {
            get { return DiffConfiguration.IsDiff; }
        }

        public override string Name
        {
            get { return "In " + DiffConfiguration.Left.Name; }
        }

        private static string InOld(DifferenceType differenceType)
        {
            switch (differenceType)
            {
                case DifferenceType.Unchanged:
                case DifferenceType.Removed:
                case DifferenceType.Changed:
                    return "Yes";

                case DifferenceType.Added:
                    return "No";

                default:
                    throw new ArgumentOutOfRangeException("differenceType");
            }
        }

        public override string GetValue(NamespaceMapping mapping)
        {
            return InOld(mapping.Difference);
        }

        public override string GetValue(TypeMapping mapping)
        {
            return InOld(mapping.Difference);
        }

        public override string GetValue(MemberMapping mapping)
        {
            return InOld(mapping.Difference);
        }
    }
}
