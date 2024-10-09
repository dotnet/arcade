// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;

namespace Microsoft.DotNet.XHarness.Apple;

public interface IExitCodeDetector
{
    int? DetectExitCode(AppBundleInformation appBundleInfo, IReadableLog systemLog);
}
public interface IiOSExitCodeDetector : IExitCodeDetector
{
}

public interface IMacCatalystExitCodeDetector : IExitCodeDetector
{
}

public abstract class ExitCodeDetector : IExitCodeDetector
{
    // This tag is logged by the dotnet/runtime Apple app wrapper
    // https://github.com/dotnet/runtime/blob/a883caa0803778084167b978281c34db8e753246/src/tasks/AppleAppBuilder/Templates/runtime.m#L30
    protected const string DotnetAppExitTag = "DOTNET.APP_EXIT_CODE:";

    // This line is logged by MacOS
    protected const string AbnormalExitMessage = "Service exited with abnormal code";

    public int? DetectExitCode(AppBundleInformation appBundleInfo, IReadableLog log)
    {
        StreamReader reader;

        try
        {
            reader = log.GetReader();
        }
        catch (FileNotFoundException e)
        {
            throw new Exception("Failed to detect application's exit code. The log file was empty / not found at " + e.FileName);
        }

        using (reader)
        while (!reader.EndOfStream)
        {
            if (reader.ReadLine() is string line
                && IsSignalLine(appBundleInfo, line) is Match match && match.Success
                && int.TryParse(match.Groups["exitCode"].Value, out var exitCode))
            {
                return exitCode;
            }
        }

        return null;
    }

    protected virtual Match? IsSignalLine(AppBundleInformation appBundleInfo, string logLine)
    {
        if (IsAbnormalExitLine(appBundleInfo, logLine) || IsStdoutExitLine(appBundleInfo, logLine))
        {
            return EoLExitCodeRegex.Match(logLine);
        }

        return null;
    }

    protected Regex EoLExitCodeRegex { get; } = new Regex(@" (?<exitCode>-?[0-9]+)$", RegexOptions.Compiled);

    // Example line coming from app's stdout log stream
    // 2022-03-18 12:48:53.336 I  Microsoft.Extensions.Configuration.CommandLine.Tests[12477:10069] DOTNET.APP_EXIT_CODE: 0
    private static bool IsStdoutExitLine(AppBundleInformation appBundleInfo, string logLine) =>
        logLine.Contains(DotnetAppExitTag) && logLine.Contains(appBundleInfo.BundleExecutable ?? appBundleInfo.BundleIdentifier);

    // Example line
    // Feb 18 06:40:16 Admins-Mac-Mini com.apple.xpc.launchd[1] (net.dot.System.Buffers.Tests.15140[59229]): Service exited with abnormal code: 74
    private static bool IsAbnormalExitLine(AppBundleInformation appBundleInfo, string logLine) =>
        logLine.Contains(AbnormalExitMessage) && (logLine.Contains(appBundleInfo.AppName) || logLine.Contains(appBundleInfo.BundleIdentifier));
}

public class iOSExitCodeDetector : ExitCodeDetector, IiOSExitCodeDetector
{
    // Example line coming from the mlaunch log
    // [07:02:21.6637600] Application 'net.dot.iOS.Simulator.PInvoke.Test' terminated (with exit code '42' and/or crashing signal ').
    private Regex DeviceExitCodeRegex { get; } = new Regex(@"terminated \(with exit code '(?<exitCode>-?[0-9]+)' and/or crashing signal", RegexOptions.Compiled);
    
    protected override Match? IsSignalLine(AppBundleInformation appBundleInfo, string logLine)
    {
        if (base.IsSignalLine(appBundleInfo, logLine) is Match match && match.Success)
        {
            return match;
        }

        if (logLine.Contains(appBundleInfo.BundleIdentifier))
        {
            return DeviceExitCodeRegex.Match(logLine);
        }

        return null;
    }
}

public class MacCatalystExitCodeDetector : ExitCodeDetector, IMacCatalystExitCodeDetector
{
}
