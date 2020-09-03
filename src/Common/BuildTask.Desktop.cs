// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Build.Common.Desktop;

namespace Microsoft.DotNet.Build.Tasks
{
    public partial class BuildTask
    {
        static BuildTask()
        {
            AssemblyResolver.Enable();
        }
    }
}
