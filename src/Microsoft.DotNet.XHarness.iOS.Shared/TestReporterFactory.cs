// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

using ExceptionLogger = System.Action<int, string>;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared;

public interface ITestReporterFactory
{
    ITestReporter Create(IFileBackedLog mainLog,
        IReadableLog runLog,
        ILogs logs,
        ICrashSnapshotReporter crashSnapshotReporter,
        ISimpleListener simpleListener,
        IResultParser parser,
        AppBundleInformation appInformation,
        RunMode runMode,
        XmlResultJargon xmlJargon,
        string? device,
        TimeSpan timeout,
        string? additionalLogsDirectory = null,
        ExceptionLogger? exceptionLogger = null,
        bool generateHtml = false);
}

public class TestReporterFactory : ITestReporterFactory
{
    private readonly IMlaunchProcessManager _processManager;

    public TestReporterFactory(IMlaunchProcessManager processManager)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
    }

    public ITestReporter Create(IFileBackedLog mainLog,
        IReadableLog runLog,
        ILogs logs,
        ICrashSnapshotReporter crashReporter,
        ISimpleListener simpleListener,
        IResultParser parser,
        AppBundleInformation appInformation,
        RunMode runMode,
        XmlResultJargon xmlJargon,
        string? device,
        TimeSpan timeout,
        string? additionalLogsDirectory = null,
        ExceptionLogger? exceptionLogger = null,
        bool generateHtml = false) => new TestReporter(_processManager,
            mainLog,
            runLog,
            logs,
            crashReporter,
            simpleListener,
            parser,
            appInformation,
            runMode,
            xmlJargon,
            device,
            timeout,
            additionalLogsDirectory,
            exceptionLogger,
            generateHtml);
}

