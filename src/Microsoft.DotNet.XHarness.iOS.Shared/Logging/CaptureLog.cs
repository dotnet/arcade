// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using Microsoft.DotNet.XHarness.Common.Logging;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Logging;

public interface ICaptureLogFactory
{
    ICaptureLog Create(string path, string systemLogPath, bool entireFile, string description = null);
    ICaptureLog Create(string path, string systemLogPath, bool entireFile, LogType logType);
}

public class CaptureLogFactory : ICaptureLogFactory
{
    public ICaptureLog Create(string path, string systemLogPath, bool entireFile, string description = null) =>
        new CaptureLog(path, systemLogPath, entireFile)
        {
            Description = description
        };

    public ICaptureLog Create(string path, string systemLogPath, bool entireFile, LogType logType)
        => Create(path, systemLogPath, entireFile, logType.ToString());
}

public interface ICaptureLog : IFileBackedLog
{
    void StartCapture();
    void StopCapture(TimeSpan? waitIfEmpty = null);
}

/// <summary>
/// A log that captures data written to a separate file between two moments in time
/// (between StartCapture and StopCapture).
/// </summary>
public class CaptureLog : FileBackedLog, ICaptureLog
{
    private readonly bool _entireFile;
    private long _startPosition;
    private long _endPosition;
    private bool _started;
    private bool _stopped;

    /// <summary>
    /// File we are watching
    /// </summary>
    public string CapturePath { get; }

    /// <summary>
    /// Destination file we are copying to
    /// </summary>
    public override string FullPath { get; }

    public CaptureLog(string destinationPath, string capturedPath, bool entireFile = false)
    {
        FullPath = destinationPath ?? throw new ArgumentNullException(nameof(destinationPath));
        CapturePath = capturedPath ?? throw new ArgumentNullException(nameof(destinationPath));
        _entireFile = entireFile;
    }

    public void StartCapture()
    {
        if (_entireFile)
        {
            return;
        }

        if (File.Exists(CapturePath))
        {
            _startPosition = new FileInfo(CapturePath).Length;
        }

        _started = true;
    }

    public void StopCapture(TimeSpan? waitIfEmpty = null)
    {
        if (_stopped)
        {
            return;
        }

        lock (CapturePath)
        {
            if (_stopped)
            {
                return;
            }

            try
            {
                if (!_started && !_entireFile)
                {
                    throw new InvalidOperationException("StartCapture() must be called before StopCature() on when the entire file is being captured.");
                }

                if (!File.Exists(CapturePath))
                {
                    File.WriteAllText(FullPath, $"Could not capture the file '{CapturePath}' because it doesn't exist.");
                    return;
                }

                _endPosition = new FileInfo(CapturePath).Length;

                if ((_endPosition == 0 || (_startPosition == _endPosition && !_entireFile)) && waitIfEmpty.HasValue)
                {
                    Thread.Sleep((int)waitIfEmpty.Value.TotalMilliseconds);
                    _endPosition = new FileInfo(CapturePath).Length;
                }

                if (_entireFile)
                {
                    File.Copy(CapturePath, FullPath, true);
                    return;
                }

                Capture();
            }
            finally
            {
                _stopped = true;
            }
        }
    }

    private void Capture()
    {
        if (_entireFile)
        {
            return;
        }

        if (!File.Exists(CapturePath))
        {
            File.AppendAllText(FullPath, $"{Environment.NewLine}Could not capture the file '{CapturePath}' because it does not exist.");
            return;
        }

        var sourceLength = new FileInfo(CapturePath).Length;

        var endPosition = _endPosition;
        if (endPosition == 0)
        {
            endPosition = sourceLength;
        }

        // The file shrank? lets copy the entire file in this case, which is better than nothing
        if (endPosition < _startPosition)
        {
            File.Copy(CapturePath, FullPath, true);
            return;
        }

        var destFile = new FileInfo(FullPath);
        var alreadyCapturedLength = destFile.Exists ? destFile.Length : 0L;
        if (alreadyCapturedLength + _startPosition >= sourceLength)
        {
            // We've captured before, and nothing new as added since last time.
            return;
        }

        var readPosition = _startPosition + alreadyCapturedLength;

        using var reader = new FileStream(CapturePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var writer = new FileStream(FullPath, FileMode.Create, FileAccess.Write, FileShare.Read);

        var buffer = new byte[4096];
        reader.Seek(readPosition, SeekOrigin.Begin);

        while (readPosition < endPosition)
        {
            int bytesRead = reader.Read(buffer, 0, (int)Math.Min(buffer.Length, endPosition - readPosition));
            if (bytesRead > 0)
            {
                writer.Write(buffer, 0, bytesRead);
                readPosition += bytesRead;
            }
            else
            {
                // There's nothing more to read.
                // I can't see how we get here, since we calculate the amount to read based on what's available, but it does happen randomly.
                break;
            }
        }
    }

    public override StreamReader GetReader()
    {
        lock (CapturePath)
        {
            // If we copied the original file over, use it as the source for reading
            // (original file might not exist anymore)
            if (_stopped)
            {
                return new StreamReader(new FileStream(FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            }
        }

        // If we want to capture something in the future that doesn't exist yet
        if (!File.Exists(CapturePath))
        {
            return null;
        }

        var stream = new FileStream(CapturePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        // Find the spot where we started capturing
        stream.Seek(_startPosition, SeekOrigin.Begin);

        return new StreamReader(stream);
    }

    public override void Flush() => Capture();

    protected override void WriteImpl(string value) => throw new InvalidOperationException();

    public override void Dispose()
    {
        StopCapture();
        GC.SuppressFinalize(this);
    }
}
