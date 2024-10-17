// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.XHarness.Android;
using Microsoft.DotNet.XHarness.Common;

namespace Microsoft.DotNet.XHarness.CLI.Android;

internal static class IDiagnosticDataExtensions
{
    public static void CaptureDeviceInfo(this IDiagnosticsData data, AndroidDevice device)
    {
        data.Target = device.Architecture;
        data.TargetOS = "API " + device.ApiVersion;
        data.Device = device.DeviceSerial;
        data.IsDevice = !device.DeviceSerial.ToLowerInvariant().StartsWith("emulator");
    }
}
