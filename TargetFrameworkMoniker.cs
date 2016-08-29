// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit.Sdk;

namespace Xunit
{
    [Flags]
    public enum TargetFrameworkMoniker
    {
        Net45 = 1,
        Net451 = 2,
        Net452 = 4,
        Net46 = 8,
        Net461 = 16,
        Net462 = 32,
        Net463 = 64,
        Netcore50 = 128,
        Netcore50aot = 256,
        Netcoreapp1_0 = 512,
        Netcoreapp1_1 = 1024
    }
}
