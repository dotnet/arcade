// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.DotNet.XHarness.Common.Execution;

public class LinuxProcessManager : UnixProcessManager
{
    [DllImport("libc")]
    private static extern int kill(int pid, int sig);

    protected override int Kill(int pid, int sig) => kill(pid, sig);
}
