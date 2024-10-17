// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Common;

public class LogWriter
{
    private readonly TextWriter _writer;
    private readonly IDevice? _device;

    public MinimumLogLevel MinimumLogLevel { get; set; } = MinimumLogLevel.Info;

    public LogWriter() : this(null, Console.Out) { }

    public LogWriter(IDevice? device) : this(device, Console.Out) { }

    public LogWriter(TextWriter w) : this(null, w) { }

    public LogWriter(IDevice? device, TextWriter writer)
    {
        _writer = writer ?? Console.Out;
        _device = device;
        if (_device is not null) // we just write the header if we do have the device info
        {
            InitLogging();
        }
    }

    [System.Runtime.InteropServices.DllImport("/usr/lib/libobjc.dylib")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    public void InitLogging()
    {
        Debug.Assert(_device is not null);
        // print some useful info
        _writer.WriteLine("[Runner executing:\t{0}]", "Run everything");
        _writer.WriteLine("[{0}:\t{1} v{2}]", _device.Model, _device.SystemName, _device.SystemVersion);
        _writer.WriteLine("[Device Name:\t{0}]", _device.Name);
        _writer.WriteLine("[Device UDID:\t{0}]", _device.UniqueIdentifier);
        _writer.WriteLine("[Device Locale:\t{0}]", _device.Locale);
        _writer.WriteLine("[Device Date/Time:\t{0}]", DateTime.Now); // to match earlier C.WL output
        _writer.WriteLine("[Bundle:\t{0}]", _device.BundleIdentifier);
    }
    public void OnError(string message)
    {
        if (MinimumLogLevel < MinimumLogLevel.Error)
        {
            return;
        }

        _writer.WriteLine(message);
        _writer.Flush();
    }

    public void OnWarning(string message)
    {
        if (MinimumLogLevel < MinimumLogLevel.Warning)
        {
            return;
        }

        _writer.WriteLine(message);
        _writer.Flush();
    }

    public void OnDebug(string message)
    {
        if (MinimumLogLevel < MinimumLogLevel.Debug)
        {
            return;
        }

        _writer.WriteLine(message);
        _writer.Flush();
    }

    public void OnDiagnostic(string message)
    {
        if (MinimumLogLevel < MinimumLogLevel.Verbose)
        {
            return;
        }

        _writer.WriteLine(message);
        _writer.Flush();
    }

    public void OnInfo(string message)
    {
        if (MinimumLogLevel < MinimumLogLevel.Info)
        {
            return;
        }

        _writer.WriteLine(message);
        _writer.Flush();
    }

    public void Info(string message)
    {
        _writer.WriteLine(message);
        _writer.Flush();
    }

}
