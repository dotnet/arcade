// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.XHarness.Common.Logging;

/// <summary>
/// A log that writes to standard output
/// </summary>
public class ConsoleLog : ReadableLog
{
    readonly StringBuilder _captured = new();

    protected override void WriteImpl(string value)
    {
        lock (_captured)
        {
            _captured.Append(value);
        }

        Console.Write(value);
    }

    public override StreamReader GetReader()
    {
        lock (_captured)
        {
            var str = new MemoryStream(Encoding.GetBytes(_captured.ToString()));
            return new StreamReader(str, Encoding, false);
        }
    }

    public override void Flush()
    {
    }

    public override void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
