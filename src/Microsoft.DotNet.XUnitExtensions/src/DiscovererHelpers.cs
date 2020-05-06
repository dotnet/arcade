// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.DotNet.XUnitExtensions
{
    internal static class DiscovererHelpers
    {
        private static readonly Lazy<bool> s_isMonoRuntime = new Lazy<bool>(() => Type.GetType("Mono.RuntimeStructs") != null);
        public static bool IsMonoRuntime => s_isMonoRuntime.Value;
        public static bool IsRunningOnNetCoreApp { get; } = (Environment.Version.Major >= 5 || RuntimeInformation.FrameworkDescription.StartsWith(".NET Core", StringComparison.OrdinalIgnoreCase));
        public static bool IsRunningOnNetFramework { get; } = RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase);

        public static bool TestPlatformApplies(TestPlatforms platforms) =>
                (platforms.HasFlag(TestPlatforms.Any)) ||
                (platforms.HasFlag(TestPlatforms.FreeBSD) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD"))) ||
                (platforms.HasFlag(TestPlatforms.Linux) && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) ||
                (platforms.HasFlag(TestPlatforms.NetBSD) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("NETBSD"))) ||
                (platforms.HasFlag(TestPlatforms.OSX) && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) ||
                (platforms.HasFlag(TestPlatforms.Windows) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        
        public static bool TestRuntimeApplies(TestRuntimes runtimes) =>
                (runtimes.HasFlag(TestRuntimes.Mono) && IsMonoRuntime) ||
                (runtimes.HasFlag(TestRuntimes.CoreCLR) && !IsMonoRuntime); // assume CoreCLR if it's not Mono

        public static bool TestFrameworkApplies(TargetFrameworkMonikers frameworks) =>
                (frameworks.HasFlag(TargetFrameworkMonikers.Netcoreapp) && IsRunningOnNetCoreApp) ||
                (frameworks.HasFlag(TargetFrameworkMonikers.NetFramework) && IsRunningOnNetFramework);

        internal static bool Evaluate(Type calleeType, string[] conditionMemberNames)
        {
            foreach (string entry in conditionMemberNames)
            {
                // Null condition member names are silently tolerated.
                if (string.IsNullOrWhiteSpace(entry)) continue;

                MethodInfo conditionMethodInfo = ConditionalTestDiscoverer.LookupConditionalMethod(calleeType, entry);
                if (conditionMethodInfo == null)
                {
                    throw new InvalidOperationException($"Unable to get MethodInfo, please check input for {entry}.");
                }

                if (!(bool)conditionMethodInfo.Invoke(null, null)) return false;
            }

            return true;
        }
    }
}
