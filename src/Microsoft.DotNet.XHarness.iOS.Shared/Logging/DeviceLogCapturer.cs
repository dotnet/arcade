// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Execution;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Logging;

public interface IDeviceLogCapturer : IDisposable
{
    void StartCapture();
    void StopCapture();
}

public class DeviceLogCapturer : IDeviceLogCapturer
{
    private readonly IMlaunchProcessManager _processManager;
    private readonly ILog _mainLog;
    private readonly ILog _deviceLog;
    private readonly string _deviceName;

    public DeviceLogCapturer(IMlaunchProcessManager processManager, ILog mainLog, ILog deviceLog, string deviceName)
    {
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _mainLog = mainLog ?? throw new ArgumentNullException(nameof(mainLog));
        _deviceLog = deviceLog ?? throw new ArgumentNullException(nameof(deviceLog));
        _deviceName = deviceName ?? throw new ArgumentNullException(nameof(deviceName));
    }

    private Process _process;

    public void StartCapture()
    {
        var args = new MlaunchArguments
            {
                new SdkRootArgument(_processManager.XcodeRoot),
                new LogDevArgument(),
                new DeviceNameArgument(_deviceName),
            };

        _process = new Process();
        _process.StartInfo.FileName = _processManager.MlaunchPath;
        _process.StartInfo.Arguments = args.AsCommandLine();
        _process.StartInfo.UseShellExecute = false;
        _process.StartInfo.RedirectStandardOutput = true;
        _process.StartInfo.RedirectStandardError = true;
        _process.StartInfo.RedirectStandardInput = true;
        _process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
        {
            if (e.Data != null)
            {
                return;
            }

            lock (_deviceLog)
            {
                _deviceLog.WriteLine(e.Data);
            }
        };

        _process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
        {
            if (e.Data == null)
            {
                return;
            }

            lock (_deviceLog)
            {
                _deviceLog.WriteLine(e.Data);
            }
        };

        _deviceLog.WriteLine("{0} {1}", _process.StartInfo.FileName, _process.StartInfo.Arguments);

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    public void StopCapture()
    {
        if (_process.HasExited)
        {
            return;
        }

        _process.StandardInput.WriteLine();
        if (_process.WaitForExit((int)TimeSpan.FromSeconds(5).TotalMilliseconds))
        {
            return;
        }

        _processManager.KillTreeAsync(_process, _mainLog, diagnostics: false).Wait();
        _process.Dispose();
    }

    public void Dispose() => StopCapture();
}

