// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.DarcLib
{
    public class VstsPush
    {
        public VstsPush(VstsRefUpdate refUpdate, VstsCommit vstsCommit)
        {
            RefUpdates = new List<VstsRefUpdate> { refUpdate };
            Commits = new List<VstsCommit> { vstsCommit };
        }
        public List<VstsRefUpdate> RefUpdates { get; set; }

        public List<VstsCommit> Commits { get; set; }
    }
}
