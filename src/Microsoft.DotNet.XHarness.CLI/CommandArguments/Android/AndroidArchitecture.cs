using System;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

internal enum AndroidArchitecture
{
    X86,
    X86_64,
    Arm64_v8a,
    Armeabi_v7a
}

internal static class AndroidArchitectureHelper
{
    public static AndroidArchitecture ParseAsAndroidArchitecture(this string target) => target switch
    {
        "x86" => AndroidArchitecture.X86,
        "x86_64" => AndroidArchitecture.X86_64,
        "arm64-v8a" => AndroidArchitecture.Arm64_v8a,
        "armeabi-v7a" => AndroidArchitecture.Armeabi_v7a,
        _ => throw new ArgumentOutOfRangeException(nameof(target))
    };

    public static string AsString(this AndroidArchitecture arch) => arch switch
    {
        AndroidArchitecture.X86 => "x86",
        AndroidArchitecture.X86_64 => "x86_64",
        AndroidArchitecture.Arm64_v8a => "arm64-v8a",
        AndroidArchitecture.Armeabi_v7a => "armeabi-v7a",
        _ => throw new ArgumentOutOfRangeException(nameof(arch))
    };
}
