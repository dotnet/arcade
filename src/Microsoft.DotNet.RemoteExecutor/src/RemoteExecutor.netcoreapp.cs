// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.RemoteExecutor
{
    /// <summary>
    /// Base class used for all tests that need to spawn a remote process.
    /// </summary>
    public static partial class RemoteExecutor
    {
        public static string HostRunnerName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dotnet.exe" : "dotnet";
        public static readonly string HostRunner = Process.GetCurrentProcess().MainModule.FileName;
        private static readonly string ExtraParameter = Path.GetFullPath("Microsoft.DotNet.RemoteExecutorHost.dll");
    }
}
