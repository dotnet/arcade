// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.DarcLib
{
    public class DependencyDetailComparer : IEqualityComparer<DependencyDetail>
    {
        public bool Equals(DependencyDetail x, DependencyDetail y)
        {
            return x.Commit == y.Commit &&
                x.Name == y.Name &&
                x.RepoUri == y.RepoUri &&
                x.Version == y.Version;
        }

        public int GetHashCode(DependencyDetail obj)
        {
            return (obj.Commit,
                obj.Name,
                obj.RepoUri,
                obj.Version).GetHashCode();
        }
    }
}
