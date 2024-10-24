// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.Android;

/// <summary>
///  Exit codes we monitor from ADB commands
/// </summary>
public enum AdbExitCodes
{
    SUCCESS = 0,
    INSTRUMENTATION_SUCCESS = -1,
    INSTRUMENTATION_TIMEOUT = -2,
    COMMAND_NOT_FOUND = 127,
    ADB_BROKEN_PIPE = 224,
    ADB_UNINSTALL_APP_NOT_ON_DEVICE = 255,
    ADB_UNINSTALL_APP_NOT_ON_EMULATOR = 1,
}
