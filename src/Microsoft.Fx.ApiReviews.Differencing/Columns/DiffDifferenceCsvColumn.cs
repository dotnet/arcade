// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Cci.Differs;
using Microsoft.Cci.Mappings;

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public sealed class DiffDifferenceCsvColumn : DiffCsvColumn
    {
        public DiffDifferenceCsvColumn(DiffConfiguration diffConfiguration)
            : base(diffConfiguration)
        {
        }

        public override bool IsVisible
        {
            get { return DiffConfiguration.IsDiff; }
        }

        public override string Name
        {
            get { return "Difference"; }
        }

        private static string Difference(DifferenceType differenceType)
        {
            switch (differenceType)
            {
                case DifferenceType.Unchanged:
                    return "Unchanged";
                case DifferenceType.Added:
                    return "Added";
                case DifferenceType.Removed:
                    return "Removed";
                case DifferenceType.Changed:
                    return "Changed";
                default:
                    throw new ArgumentOutOfRangeException("differenceType");
            }
        }

        public override string GetValue(NamespaceMapping mapping)
        {
            return Difference(mapping.Difference);
        }

        public override string GetValue(TypeMapping mapping)
        {
            return Difference(mapping.Difference);
        }

        public override string GetValue(MemberMapping mapping)
        {
            return Difference(mapping.Difference);
        }
    }
}
