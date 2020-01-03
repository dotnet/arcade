// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Cci.Extensions;

namespace Microsoft.Cci.Filters
{
    public class ExcludeAttributesFilter : IncludeAllFilter
    {
        private readonly HashSet<string> _attributeDocIds;

        public ExcludeAttributesFilter(IEnumerable<string> attributeDocIds, bool includeForwardedTypes = false)
        {
            _attributeDocIds = new HashSet<string>(attributeDocIds);
            IncludeForwardedTypes = includeForwardedTypes;
        }

        public ExcludeAttributesFilter(string attributeDocIdFile, bool includeForwardedTypes = false)
        {
            _attributeDocIds = new HashSet<string>(DocIdExtensions.ReadDocIds(attributeDocIdFile));
            IncludeForwardedTypes = includeForwardedTypes;
        }

        // Exists to avoid breaking change in removing includeForwardedTypes constructor parameters. This is a bit
        // less ugly than suppressing IDE0060.
        public bool IncludeForwardedTypes { get; }

        public override bool Include(ICustomAttribute attribute)
        {
            if (_attributeDocIds.Contains(attribute.DocId()))
                return false;

            return base.Include(attribute);
        }
    }
}
