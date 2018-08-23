// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Cci.Comparers
{
    public interface ICciComparers
    {
        IEqualityComparer<T> GetEqualityComparer<T>();
        IComparer<T> GetComparer<T>();
    }
}
