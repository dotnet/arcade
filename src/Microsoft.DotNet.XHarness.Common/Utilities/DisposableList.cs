// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.XHarness.Common.Utilities;

public class DisposableList<T> : List<T>, IDisposable where T : IDisposable
{
    public void Dispose()
    {
        foreach (var item in this)
        {
            item.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
