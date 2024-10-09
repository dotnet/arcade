// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.DotNet.XHarness.Common.CLI;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;

namespace Microsoft.DotNet.XHarness.Apple;

public class ErrorKnowledgeBase : IErrorKnowledgeBase
{
    private static readonly Dictionary<string, KnownIssue> s_testErrorMaps = new()
    {
        ["Failed to communicate with the device"] =
            new("Failed to communicate with the device. Please ensure the cable is properly connected, and try rebooting the device",
                suggestedExitCode: (int)ExitCode.DEVICE_FAILURE),

        ["MT1031"] =
            new("Cannot launch the application because the device is locked. Please unlock the device and try again",
                suggestedExitCode: (int)ExitCode.DEVICE_FAILURE),

        ["the device is locked"] =
            new("Cannot launch the application because the device is locked. Please unlock the device and try again",
                suggestedExitCode: (int)ExitCode.DEVICE_FAILURE),

        ["while Setup Assistant is running"] =
            new("Cannot launch the application because the device's update hasn't been finished. The setup assistant is still running. Please finish the device OS update on the device",
                suggestedExitCode: (int)ExitCode.DEVICE_FAILURE),

        ["LSOpenURLsWithRole() failed with error -10825"] =
            new("This application requires a newer version of MacOS",
                suggestedExitCode: (int)ExitCode.GENERAL_FAILURE),

        ["Failed to start launchd_sim: could not bind to session"] =
            new("Failed to launch the Simulator as XHarness was most likely started from a user session without GUI capabilities (e.g. from a launchd daemon). " +
                "Please start XHarness from a full user session or bind the run to one via `sudo launchctl asuser`",
                suggestedExitCode: (int)ExitCode.APP_LAUNCH_FAILURE),

        ["error HE0018: Could not launch the simulator application"] =
            new("Failed to launch the Simulator, please try again. If the problem persists, try rebooting MacOS",
                suggestedExitCode: (int)ExitCode.SIMULATOR_FAILURE),

        ["error HE0042: Could not launch the app"] =
            new("Failed to launch the application, please try again. If the problem persists, try rebooting MacOS",
                suggestedExitCode: (int)ExitCode.APP_LAUNCH_FAILURE),
       
        ["[TCP tunnel] Xamarin.Hosting: Failed to connect to port"] = new(
            "TCP Tunnel Connection Failed to connect to TCP port",
            suggestedExitCode: (int)ExitCode.TCP_CONNECTION_FAILED),
    };

    private static readonly Dictionary<string, KnownIssue> s_buildErrorMaps = new();

    private static readonly Dictionary<string, KnownIssue> s_installErrorMaps = new()
    {
        ["IncorrectArchitecture"] =
            new("IncorrectArchitecture: Failed to find matching device arch for the application"), // known failure, but not an issue

        ["0xe8008015"] =
            new("No valid provisioning profile found", suggestedExitCode: (int)ExitCode.APP_NOT_SIGNED),

        ["valid provisioning profile for this executable was not found"] =
            new("No valid provisioning profile found", suggestedExitCode: (int)ExitCode.APP_NOT_SIGNED),

        ["0xe800801c"] =
            new("App is not signed", suggestedExitCode: (int)ExitCode.APP_NOT_SIGNED),

        ["No code signature found"] =
            new("App is not signed", suggestedExitCode: (int)ExitCode.APP_NOT_SIGNED),
    };

    public bool IsKnownBuildIssue(IFileBackedLog buildLog, [NotNullWhen(true)] out KnownIssue? knownFailureMessage)
        => TryFindErrors(buildLog, s_buildErrorMaps, out knownFailureMessage);

    public bool IsKnownTestIssue(IFileBackedLog runLog, [NotNullWhen(true)] out KnownIssue? knownFailureMessage)
        => TryFindErrors(runLog, s_testErrorMaps, out knownFailureMessage);

    public bool IsKnownInstallIssue(IFileBackedLog installLog, [NotNullWhen(true)] out KnownIssue? knownFailureMessage)
        => TryFindErrors(installLog, s_installErrorMaps, out knownFailureMessage);

    private static bool TryFindErrors(IFileBackedLog log, Dictionary<string, KnownIssue> errorMap,
        [NotNullWhen(true)] out KnownIssue? failureMessage)
    {
        failureMessage = null;
        if (log == null)
        {
            return false;
        }

        if (!File.Exists(log.FullPath) || new FileInfo(log.FullPath).Length <= 0)
        {
            return false;
        }

        using var reader = log.GetReader();
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line == null)
            {
                continue;
            }

            //go over errors and return true as soon as we find one that matches
            foreach (var error in errorMap.Keys)
            {
                if (!line.Contains(error, StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                failureMessage = errorMap[error];
                return true;
            }
        }

        return false;
    }
}
