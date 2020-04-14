// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public sealed class DiffDocument
    {
        public DiffDocument(AssemblySet left, AssemblySet right, IEnumerable<DiffLine> lines, IEnumerable<DiffApiDefinition> apiDefinitions)
        {
            Left = left;
            Right = right;
            Lines = new ReadOnlyCollection<DiffLine>(lines.ToArray());
            ApiDefinitions = new ReadOnlyCollection<DiffApiDefinition>(apiDefinitions.ToArray());
        }

        public bool IsEmpty
        {
            get
            {
                return (Left == null || Left.IsEmpty) &&
                       (Right == null || Right.IsEmpty);
            }
        }

        public bool IsDiff
        {
            get
            {
                var hasLeft = Left != null && !Left.IsEmpty;
                var hasRight = Right != null && !Right.IsEmpty;
                if (!hasLeft && !hasRight)
                    return false;

                return !hasLeft || hasRight;
            }
        }

        public AssemblySet Left { get; private set; }
        public AssemblySet Right { get; private set; }
        public ReadOnlyCollection<DiffLine> Lines { get; private set; }
        public ReadOnlyCollection<DiffApiDefinition> ApiDefinitions { get; private set; }
    }
}
