// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;

namespace Microsoft.DotNet.XHarness.Apple;

public interface IDeviceLogCapturerFactory
{
    IDeviceLogCapturer Create(ILog mainLog, ILog deviceLog, string deviceName);
}

public class DeviceLogCapturerFactory : IDeviceLogCapturerFactory
{
    private readonly IMlaunchProcessManager _processManager;

    public DeviceLogCapturerFactory(IMlaunchProcessManager processManager)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
    }

    public IDeviceLogCapturer Create(ILog mainLog, ILog deviceLog, string deviceName) => new DeviceLogCapturer(_processManager, mainLog, deviceLog, deviceName);
}

