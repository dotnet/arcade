// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.XHarness.Common.Utilities;

public static class Extensions
{
    // Returns false if timed out
    public static async Task<bool> TimeoutAfter(this Task task, TimeSpan timeout)
    {
        if (timeout.Ticks < -1)
        {
            return false;
        }

        if (task == await Task.WhenAny(task, Task.Delay(timeout)))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    // Returns false if timed out
    public static async Task<bool> TimeoutAfter<T>(this Task<T> task, TimeSpan timeout)
    {
        if (timeout.Ticks < -1)
        {
            return false;
        }

        if (task == await Task.WhenAny(task, Task.Delay(timeout)))
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
