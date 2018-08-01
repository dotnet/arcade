// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.DarcLib
{
    public class VstsCommit
    {
        public VstsCommit(List<VstsChange> changes, string commitComment)
        {
            Changes = changes;
            Comment = commitComment;
        }
        public List<VstsChange> Changes { get; set; }

        public string Comment { get; set; }
    }
}
