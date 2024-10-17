// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.Common.CLI;

/// <summary>
/// Exit codes to use for common failure reasons; if you add a new exit code, add it here and use the enum.
/// The first part conforms with xUnit: https://xunit.net/docs/getting-started/netfx/visual-studio
/// </summary>
public enum ExitCode
{
    /// <summary>
    /// The tests ran successfully
    /// </summary>
    SUCCESS = 0,

    /// <summary>
    /// One or more of the tests failed
    /// </summary>
    TESTS_FAILED = 1,

    /// <summary>
    /// The help page was shown
    /// Either because it was requested, or because the user did not provide any command line arguments
    /// </summary>
    HELP_SHOWN = 2,

    /// <summary>
    /// There was a problem with one of the command line options
    /// </summary>
    INVALID_ARGUMENTS = 3,

    /// <summary>
    /// There was a problem loading one or more of the test packages
    /// </summary>
    PACKAGE_NOT_FOUND = 4,

    /// <summary>
    /// Time out based on the --timeout settings
    /// </summary>
    TIMED_OUT = 70,

    /// <summary>
    /// Generic code for cases where we couldn't determine the exact cause
    /// </summary>
    GENERAL_FAILURE = 71,

    /// <summary>
    /// App installation failed
    /// </summary>
    PACKAGE_INSTALLATION_FAILURE = 78,

    /// <summary>
    /// Failed to open/parse Info.plist inside of the app bundle
    /// </summary>
    FAILED_TO_GET_BUNDLE_INFO = 79,

    /// <summary>
    /// The app was launched but we never heard from it and similar cases
    /// </summary>
    APP_CRASH = 80,

    /// <summary>
    /// XHarness failed to find a suitable target for the test
    /// </summary>
    DEVICE_NOT_FOUND = 81,

    /// <summary>
    /// Various scenarios that depend on an exit code which was not returned
    /// </summary>
    RETURN_CODE_NOT_SET = 82,

    /// <summary>
    /// An error occurred when trying to launch the application
    /// </summary>
    APP_LAUNCH_FAILURE = 83,

    /// <summary>
    /// Failed to retrieve a file from the Android device/emulator
    /// </summary>
    DEVICE_FILE_COPY_FAILURE = 84,

    /// <summary>
    /// Time outs happening during the installation phase (or install command)
    /// </summary>
    PACKAGE_INSTALLATION_TIMEOUT = 86,

    /// <summary>
    /// Apple app is not signed, provisioning profile is missing and similar
    /// </summary>
    APP_NOT_SIGNED = 87,

    /// <summary>
    /// Failed to start simulator (happens every now and then on MacOS mostly)
    /// </summary>
    SIMULATOR_FAILURE = 88,

    /// <summary>
    /// Hardware device is in some corrupted state, or just locked screen
    /// </summary>
    DEVICE_FAILURE = 89,

    /// <summary>
    /// This timeout occurs when the Apple app has been launched but hasn't
    /// connected yet over TCP and --launch-timeout expires
    /// </summary>
    APP_LAUNCH_TIMEOUT = 90,

    /// <summary>
    /// Failure when calling ADB
    /// </summary>
    ADB_FAILURE = 91,

    /// <summary>
    /// Failure when TCP tunnel between XHarness and the device fail
    /// or the device cannot connect to TCP tunnel
    /// </summary>
    TCP_CONNECTION_FAILED = 92
}
