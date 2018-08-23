// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci.Mappings;
using Microsoft.Cci.Differs;

namespace Microsoft.Cci.Filters
{
    public interface IMappingDifferenceFilter
    {
        bool Include(AssemblyMapping assembly);
        bool Include(NamespaceMapping ns);
        bool Include(TypeMapping type);
        bool Include(MemberMapping member);
        bool Include(DifferenceType difference);
    }
}
