using System.Runtime.InteropServices;
using Xunit;

namespace Microsoft.DotNet.XUnitExtensions
{
    internal static class DiscovererHelpers
    {
        internal static bool TestPlatformApplies(TestPlatforms platforms) =>
                (platforms.HasFlag(TestPlatforms.FreeBSD) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD"))) ||
                (platforms.HasFlag(TestPlatforms.Linux) && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) ||
                (platforms.HasFlag(TestPlatforms.NetBSD) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("NETBSD"))) ||
                (platforms.HasFlag(TestPlatforms.OSX) && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) ||
                (platforms.HasFlag(TestPlatforms.Windows) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        
        internal static bool TestRuntimeApplies(TestRuntimes runtimes) =>
                (runtimes.HasFlag(TestRuntimes.Mono) && SkipOnMonoDiscoverer.IsMonoRuntime) ||
                (runtimes.HasFlag(TestRuntimes.CoreCLR) && !SkipOnMonoDiscoverer.IsMonoRuntime); // assume CoreCLR if it's not Mono

    }
}
