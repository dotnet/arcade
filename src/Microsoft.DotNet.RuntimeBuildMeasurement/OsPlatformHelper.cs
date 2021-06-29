using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.RuntimeBuildMeasurement
{
    public static class OsPlatformHelper
    {
        public static OsPlatform GetCurrentPlatform()
            => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? OsPlatform.Windows
                : (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? OsPlatform.Linux
                    : OsPlatform.Mac);

        public static DirectoryInfo GetPlatformDefaultDirectory()
            => new(GetCurrentPlatform() == OsPlatform.Windows ? @"C:\Source\runtime2" : "/mnt/runtime");

        public static string GetBuildCommand()
            => GetCurrentPlatform() == OsPlatform.Windows ? "build.cmd" : "build.sh";
    }
}