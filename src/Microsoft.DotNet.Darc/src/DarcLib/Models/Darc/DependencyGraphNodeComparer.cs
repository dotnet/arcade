// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.DarcLib
{
    public class DependencyGraphNodeComparer : IEqualityComparer<DependencyGraphNode>
    {
        public bool Equals(DependencyGraphNode x, DependencyGraphNode y)
        {
            return x.DependencyDetail.Commit == y.DependencyDetail.Commit &&
                x.DependencyDetail.Name == y.DependencyDetail.Name &&
                x.DependencyDetail.RepoUri == y.DependencyDetail.RepoUri &&
                x.DependencyDetail.Version == y.DependencyDetail.Version &&
                x.ChildNodes.SetEquals(y.ChildNodes);
        }

        public int GetHashCode(DependencyGraphNode obj)
        {
            return (obj.DependencyDetail.Commit,
                obj.DependencyDetail.Name,
                obj.DependencyDetail.RepoUri,
                obj.DependencyDetail.Version).GetHashCode();
        }
    }
}
