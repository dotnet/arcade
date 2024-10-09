// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.Common.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Logging;

public class LogFile : FileBackedLog
{
    private readonly object _lockObj = new();

    public override string FullPath { get; }

    private FileStream _writer;
    private bool _disposed;

    public LogFile(string description, string path, bool append = true)
        : base(description)
    {
        FullPath = path ?? throw new ArgumentNullException(nameof(path));
        if (!append)
        {
            File.WriteAllText(path, string.Empty);
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        try
        {
            // We don't want to open the file every time someone writes to the log, so we keep it as an instance
            // variable until we're disposed. Due to the async nature of how we run tests, writes may still
            // happen after we're disposed, in which case we create a temporary stream we close after writing
            lock (_lockObj)
            {
                var fs = _writer ?? new FileStream(FullPath, FileMode.Append, FileAccess.Write, FileShare.Read);

                fs.Write(buffer, offset, count);

                if (_disposed)
                {
                    fs.Dispose();
                }
                else
                {
                    _writer = fs;
                }
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Failed to write to the file {FullPath}: {e}.");
            return;
        }
    }

    public override void Flush()
    {
        if (!_disposed)
        {
            _writer?.Flush();
        }
    }

    protected override void WriteImpl(string value)
    {
        var bytes = Encoding.GetBytes(value);
        Write(bytes, 0, bytes.Length);
    }

    public override StreamReader GetReader() => new(new FileStream(FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

    public override void Dispose()
    {
        lock (_lockObj)
        {
            if (!_disposed)
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
            }
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
