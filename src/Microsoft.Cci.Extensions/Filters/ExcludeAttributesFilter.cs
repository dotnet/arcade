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

        public ExcludeAttributesFilter(IEnumerable<string> attributeDocIds)
        {
            _attributeDocIds = new HashSet<string>(attributeDocIds);
        }

        public ExcludeAttributesFilter(string attributeDocIdFile)
        {
            _attributeDocIds = new HashSet<string>(DocIdExtensions.ReadDocIds(attributeDocIdFile));
        }

        public override bool Include(ICustomAttribute attribute)
        {
            if (_attributeDocIds.Contains(attribute.DocId()))
                return false;

            return base.Include(attribute);
        }
    }
}
