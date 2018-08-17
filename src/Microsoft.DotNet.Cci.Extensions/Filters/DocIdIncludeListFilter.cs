// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.DotNet.Cci.Extensions;
using System.IO;

namespace Microsoft.Cci.Filters
{
    public class DocIdIncludeListFilter : ICciFilter
    {
        private readonly HashSet<string> _docIds;

        public DocIdIncludeListFilter(IEnumerable<string> docIds)
        {
            _docIds = new HashSet<string>(docIds);
        }

        public DocIdIncludeListFilter(string whiteListFilePath)
        {
            _docIds = DocIdExtensions.ReadDocIds(whiteListFilePath);
        }

        public bool AlwaysIncludeNonEmptyTypes { get; set; }

        public bool Include(INamespaceDefinition ns)
        {
            // Only include non-empty namespaces
            if (!ns.GetTypes().Any(Include))
                return false;

            string namespaceId = ns.DocId();
            return _docIds.Contains(namespaceId);
        }

        public bool Include(ITypeDefinition type)
        {
            if (AlwaysIncludeNonEmptyTypes && type.Members.Any(Include))
                return true;

            string typeId = type.DocId();
            return _docIds.Contains(typeId);
        }

        public bool Include(ITypeDefinitionMember member)
        {
            string memberId = member.DocId();
            return _docIds.Contains(memberId);
        }

        public bool Include(ICustomAttribute attribute)
        {
            string typeId = attribute.DocId();
            string removeUsages = "RemoveUsages:" + typeId;

            if (_docIds.Contains(removeUsages))
                return false;

            return _docIds.Contains(typeId);
        }
    }
}
