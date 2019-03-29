// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.RemoteExecutor
{
    /// <summary>
    /// Base class used for all tests that need to spawn a remote process.
    /// </summary>
    public static partial class RemoteExecutor
    {
        public static readonly string HostRunnerName = "Microsoft.DotNet.XUnitRunnerUap.exe";
        public static readonly string HostRunner = "Microsoft.DotNet.XUnitRunnerUap.exe";
        private static readonly string ExtraParameter = "remote";
    }
}
