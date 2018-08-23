// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Differs;

namespace Microsoft.Cci.Filters
{
    public class DifferenceFilter<T> : IDifferenceFilter where T : Difference
    {
        public virtual bool Include(Difference difference)
        {
            return difference is T;
        }
    }
}
