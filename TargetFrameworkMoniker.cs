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
        Net45 = 0x1,
        Net451 = 0x2,
        Net452 = 0x4,
        Net46 = 0x8,
        Net461 = 0x10,
        Net462 = 0x20,
        Net463 = 0x40,

        Netcore50 = 0x80,
        Netcore50aot = 0x100,

        Netcoreapp1_0 = 0x200,
        Netcoreapp1_1 = 0x400,

        NetFramework = Net45 | Net451 | Net452 | Net46 | Net461 | Net462 | Net463,
        NetFramework45 = Net45 | Net451 | Net452,
        NetFramework46 = Net46 | Net461 | Net462,

        Netcoreapp = Netcoreapp1_0 | Netcoreapp1_1,

        NetcoreUwp = Netcore50 | Netcore50aot
    }
}
