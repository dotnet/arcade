// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Android;

/// <summary>
/// Folder to copy off for output of executing the specified APK
/// </summary>
internal class DeviceOutputFolderArgument : PathArgument
{
    public DeviceOutputFolderArgument()
        : base("device-out-folder=|dev-out=", "If specified, copy this folder recursively off the device to the path specified by the output directory", false)
    {
    }
}
