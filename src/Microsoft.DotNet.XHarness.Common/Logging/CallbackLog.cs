// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Microsoft.DotNet.XHarness.Common.Logging;

/// <summary>
/// A log that forwards all written data to a callback
/// </summary>
public class CallbackLog : Log
{
    private readonly Action<string> _onWrite;
    private readonly StringBuilder _captured = new();

    public CallbackLog(Action<string> onWrite)
        : base("Callback log")
    {
        _onWrite = onWrite;
        Timestamp = false;
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public override void Flush()
    {
    }

    protected override void WriteImpl(string value)
    {
        lock (_captured)
        {
            _captured.Append(value);
        }

        _onWrite(value);
    }
}
