using System;

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

/// <summary>
/// If specified, attempt to run on a compatible attached device, failing if unavailable.
/// If not specified, we will open the apk using Zip APIs and guess what's usable based off folders found in under /lib
/// </summary>
internal class DeviceArchitectureArgument : RepeatableArgument
{
    public DeviceArchitectureArgument()
        : base("device-arch=",
            "If specified, forces running on a device with given or compatible architecture (x86, x86_64, arm64-v8a or armeabi-v7a). Otherwise inferred from supplied APK")
    {
    }

    public override void Validate()
    {
        foreach (var archName in Value)
        {
            try
            {
                AndroidArchitectureHelper.ParseAsAndroidArchitecture(archName);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ArgumentException(
                    $"Failed to parse architecture '{archName}'. Available architectures are:" +
                    GetAllowedValues<AndroidArchitecture>(t => t.AsString()));
            }
        }
    }
}
