// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Microsoft.DotNet.XHarness.Common.Logging;

public abstract partial class Log : ILog
{
    public virtual Encoding Encoding => Encoding.UTF8;
    public string? Description { get; set; }
    public virtual bool Timestamp { get; set; } = true;

    protected Log(string? description = null)
    {
        Description = description;
    }

    public virtual void Write(byte[] buffer, int offset, int count) => Write(Encoding.GetString(buffer, offset, count));

    public void Write(string value)
    {
        if (Timestamp)
        {
            value = "[" + DateTime.Now.ToString("HH:mm:ss.fffffff") + "] " + value;
        }

        WriteImpl(value);
    }

    public void WriteLine(string value) => Write(value + "\n");

    public void WriteLine(string format, params object[] args) => Write(string.Format(format, args) + "\n");

    public override string ToString() => Description ?? string.Empty;

    public abstract void Flush();

    public abstract void Dispose();

    protected abstract void WriteImpl(string value);
}
