// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Fx.ApiReviews.Differencing
{
    [Flags]
    public enum DiffStyle
    {
        None = 0x00,
        Added = 0x01,
        Removed = 0x02,
        InterfaceMember = 0x04,
        InheritedMember = 0x08,
        NotCompatible = 0x10,
    }
}
