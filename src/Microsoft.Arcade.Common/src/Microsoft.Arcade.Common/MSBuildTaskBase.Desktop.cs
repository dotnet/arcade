// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Build.Common.Desktop;

namespace Microsoft.Arcade.Common
{
    public partial class MSBuildTaskBase
    {
        static MSBuildTaskBase()
        {
            AssemblyResolver.Enable();
        }
    }
}
