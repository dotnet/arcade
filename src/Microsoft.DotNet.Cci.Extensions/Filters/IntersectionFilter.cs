// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Cci.Filters
{
    /// <summary>
    /// Combines multiple filters together to only include if all filters include.
    /// </summary>
    public class IntersectionFilter : ICciFilter
    {
        public IntersectionFilter(params ICciFilter[] filters)
        {
            Filters = new List<ICciFilter>(filters);
        }

        public IList<ICciFilter> Filters { get; private set; }

        public bool Include(ITypeDefinitionMember member)
        {
            return Filters.All(filter => filter.Include(member));
        }

        public bool Include(ICustomAttribute attribute)
        {
            return Filters.All(filter => filter.Include(attribute));
        }

        public bool Include(ITypeDefinition type)
        {
            return Filters.All(filter => filter.Include(type));
        }

        public bool Include(INamespaceDefinition ns)
        {
            return Filters.All(filter => filter.Include(ns));
        }
    }
}
