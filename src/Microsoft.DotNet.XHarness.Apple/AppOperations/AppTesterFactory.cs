// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.Apple;

public interface IAppTesterFactory
{
    IAppTester Create(CommunicationChannel communicationChannel, bool isSimulator, IFileBackedLog log, ILogs logs, Action<string>? logCallback);
}

public class AppTesterFactory : IAppTesterFactory
{
    private readonly IMlaunchProcessManager _processManager;
    private readonly ICrashSnapshotReporterFactory _snapshotReporterFactory;
    private readonly ICaptureLogFactory _captureLogFactory;
    private readonly IDeviceLogCapturerFactory _deviceLogCapturerFactory;
    private readonly ITestReporterFactory _reporterFactory;
    private readonly IResultParser _resultParser;
    private readonly IHelpers _helpers;

    public AppTesterFactory(
        IMlaunchProcessManager processManager,
        ICrashSnapshotReporterFactory snapshotReporterFactory,
        ICaptureLogFactory captureLogFactory,
        IDeviceLogCapturerFactory deviceLogCapturerFactory,
        ITestReporterFactory reporterFactory,
        IResultParser resultParser,
        IHelpers helpers)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _snapshotReporterFactory = snapshotReporterFactory ?? throw new ArgumentNullException(nameof(snapshotReporterFactory));
        _captureLogFactory = captureLogFactory ?? throw new ArgumentNullException(nameof(captureLogFactory));
        _deviceLogCapturerFactory = deviceLogCapturerFactory ?? throw new ArgumentNullException(nameof(deviceLogCapturerFactory));
        _reporterFactory = reporterFactory ?? throw new ArgumentNullException(nameof(reporterFactory));
        _resultParser = resultParser ?? throw new ArgumentNullException(nameof(resultParser));
        _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
    }

    public IAppTester Create(
        CommunicationChannel communicationChannel,
        bool isSimulator,
        IFileBackedLog log,
        ILogs logs,
        Action<string>? logCallback)
    {
        var tunnelBore = (communicationChannel == CommunicationChannel.UsbTunnel && !isSimulator)
            ? new TunnelBore(_processManager)
            : null;

        return new AppTester(
            _processManager,
            new SimpleListenerFactory(tunnelBore),
            _snapshotReporterFactory,
            _captureLogFactory,
            _deviceLogCapturerFactory,
            _reporterFactory,
            _resultParser,
            log,
            logs,
            _helpers,
            logCallback);
    }
}
