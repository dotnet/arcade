// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public sealed class DiffLine
    {
        public DiffLine(DiffLineKind kind, IEnumerable<DiffToken> tokens)
        {
            Kind = kind;
            Tokens = new ReadOnlyCollection<DiffToken>(tokens.ToArray());
        }

        public DiffLineKind Kind { get; private set; }
        public ReadOnlyCollection<DiffToken> Tokens { get; private set; }
        
        public override string ToString()
        {
            return string.Concat(Tokens);
        }
    }
}
