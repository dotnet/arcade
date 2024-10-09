// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Microsoft.DotNet.XHarness.Common.Logging;

/// <summary>
/// Log that discards everything
/// </summary>
public class NullLog : ILog
{
    public string? Description { get; set; } = "NullLog";
    public bool Timestamp { get; set; }

    public Encoding Encoding => Encoding.UTF8;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public void Flush()
    {
    }

    public void Write(byte[] buffer, int offset, int count)
    {
    }

    public void Write(string value)
    {
    }

    public void WriteLine(string value)
    {
    }

    public static void WriteLine(StringBuilder value)
    {
    }

    public void WriteLine(string format, params object[] args)
    {
    }
}
