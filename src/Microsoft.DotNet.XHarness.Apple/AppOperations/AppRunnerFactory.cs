// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Utilities;

namespace Microsoft.DotNet.XHarness.Apple;

public interface IAppRunnerFactory
{
    IAppRunner Create(IFileBackedLog log, ILogs logs, Action<string>? logCallback);
}

public class AppRunnerFactory : IAppRunnerFactory
{
    private readonly IMlaunchProcessManager _processManager;
    private readonly ICrashSnapshotReporterFactory _snapshotReporterFactory;
    private readonly ICaptureLogFactory _captureLogFactory;
    private readonly IDeviceLogCapturerFactory _deviceLogCapturerFactory;
    private readonly IHelpers _helpers;

    public AppRunnerFactory(
        IMlaunchProcessManager processManager,
        ICrashSnapshotReporterFactory snapshotReporterFactory,
        ICaptureLogFactory captureLogFactory,
        IDeviceLogCapturerFactory deviceLogCapturerFactory,
        IHelpers helpers)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _snapshotReporterFactory = snapshotReporterFactory ?? throw new ArgumentNullException(nameof(snapshotReporterFactory));
        _captureLogFactory = captureLogFactory ?? throw new ArgumentNullException(nameof(captureLogFactory));
        _deviceLogCapturerFactory = deviceLogCapturerFactory ?? throw new ArgumentNullException(nameof(deviceLogCapturerFactory));
        _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
    }

    public IAppRunner Create(IFileBackedLog log, ILogs logs, Action<string>? logCallback) =>
        new AppRunner(_processManager, _snapshotReporterFactory, _captureLogFactory, _deviceLogCapturerFactory, log, logs, _helpers, logCallback);
}
