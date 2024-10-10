// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.XHarness.Common.Execution;

public static class ProcessManagerFactory
{
    public static IProcessManager CreateProcessManager()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxProcessManager();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacOSProcessManager();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsProcessManager();
        }

        throw new InvalidOperationException("Unsupported OS platform detected when creating ProcessManager");
    }
}
