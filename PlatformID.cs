// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit.Sdk;

namespace Xunit
{
    [Flags]
    public enum PlatformID
    {
        Windows = 1,
        Linux = 2,
        OSX = 4,
        FreeBSD = 8,
        AnyUnix = FreeBSD | Linux | OSX,
        Any = ~0
    }
}
