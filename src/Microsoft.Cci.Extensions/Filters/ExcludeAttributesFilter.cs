// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.Cci.Extensions;

namespace Microsoft.Cci.Filters
{
    public class ExcludeAttributesFilter : PublicOnlyCciFilter
    {
        private readonly HashSet<string> _attributeDocIds;

        public ExcludeAttributesFilter(IEnumerable<string> attributeDocIds, bool includeForwardedTypes = false)
            : base(excludeAttributes: false, includeForwardedTypes: includeForwardedTypes)
        {
            _attributeDocIds = new HashSet<string>(attributeDocIds);
        }

        public ExcludeAttributesFilter(string attributeDocIdFile, bool includeForwardedTypes = false)
            : base(excludeAttributes: false, includeForwardedTypes: includeForwardedTypes)
        {
            _attributeDocIds = DocIdExtensions.ReadDocIds(attributeDocIdFile);
        }

        public override bool Include(ICustomAttribute attribute)
        {
            if (_attributeDocIds.Contains(attribute.DocId()))
                return false;

            return base.Include(attribute);
        }
    }
}
