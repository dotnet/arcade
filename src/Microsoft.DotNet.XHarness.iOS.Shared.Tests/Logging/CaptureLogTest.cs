// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Logging;

public class CaptureLogTest : IDisposable
{
    private readonly string _sourcePath;
    private readonly string _destinationPath;

    public CaptureLogTest()
    {
        _sourcePath = Path.GetTempFileName();
        _destinationPath = Path.GetTempFileName();
        File.Delete(_sourcePath);
        File.Delete(_destinationPath);
    }

    public void Dispose()
    {
        if (File.Exists(_sourcePath))
        {
            File.Delete(_sourcePath);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ConstructorNullFilePath()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            var captureLog = new CaptureLog(null, _sourcePath, false);
        });
    }

    [Fact]
    public void CapturePartOfTheFileOnly()
    {
        var ignoredLine = "This line should not be captured";
        var logLines = new[] { "first line", "second line", "third line" };
        File.WriteAllLines(_sourcePath, new[] { ignoredLine });

        using var captureLog = new CaptureLog(_destinationPath, _sourcePath, false);

        captureLog.StartCapture();
        File.AppendAllLines(_sourcePath, logLines);
        captureLog.StopCapture();
        File.AppendAllLines(_sourcePath, new[] { ignoredLine });

        // get the stream and assert we do have the correct lines
        using var captureStream = captureLog.GetReader();
        string logLine;
        while ((logLine = captureStream.ReadLine()) != null)
        {
            Assert.NotEqual(ignoredLine, logLine);

            if (!string.IsNullOrEmpty(logLine))
            {
                Assert.Contains(logLine, logLines);
            }
        }
    }

    [Fact]
    public void CapturePieceByPiece()
    {
        var ignoredLine = "This line should not be captured";
        var logLines = new[] { "first line", "second line", "third line" };

        File.WriteAllLines(_sourcePath, new[] { ignoredLine });

        using var captureLog = new CaptureLog(_destinationPath, _sourcePath, false);
        captureLog.StartCapture();

        File.AppendAllLines(_destinationPath, logLines.Take(1));
        captureLog.Flush();
        Assert.Contains(logLines.First(), File.ReadAllText(_destinationPath));

        File.AppendAllLines(_destinationPath, logLines.Skip(1));

        captureLog.StopCapture();

        // Get the stream and assert we do have the correct lines
        using var captureStream = captureLog.GetReader();
        string logLine;
        while ((logLine = captureStream.ReadLine()) != null)
        {
            Assert.NotEqual(ignoredLine, logLine);

            if (!string.IsNullOrEmpty(logLine))
            {
                Assert.Contains(logLine, logLines);
            }
        }
    }

    [Fact]
    public void CaptureMissingFileTest()
    {
        using (var captureLog = new CaptureLog(_destinationPath, _sourcePath, false))
        {
            Assert.Equal(_destinationPath, captureLog.FullPath);
            captureLog.StartCapture();
            captureLog.StopCapture();
        }

        // Read the data that was added to the capture path and  ensure that we do have the name of the missing file
        using (var reader = new StreamReader(_destinationPath))
        {
            var line = reader.ReadLine();
            Assert.Contains(_sourcePath, line);
        }
    }

    [Fact]
    public void CaptureWrongOrder()
    {
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var captureLog = new CaptureLog(_destinationPath, _sourcePath, false);
            captureLog.StopCapture();
        });
    }

    [Fact]
    public void CaptureEverythingAtOnce()
    {
        var logLines = new[] { "first line", "second line", "third line" };
        File.WriteAllText(_sourcePath, string.Empty);

        using var captureLog = new CaptureLog(_destinationPath, _sourcePath, false);

        captureLog.StartCapture();
        File.AppendAllLines(_sourcePath, logLines);
        captureLog.StopCapture();

        // get the stream and assert we do have the correct lines
        using var captureStream = captureLog.GetReader();
        string logLine;
        while ((logLine = captureStream.ReadLine()) != null)
        {
            if (!string.IsNullOrEmpty(logLine))
            {
                Assert.Contains(logLine, logLines);
            }
        }
    }

    [Fact]
    public void CaptureEntireFile()
    {
        var ignoredLine = "This line should not be captured";
        var logLines = new List<string>() { "first line", "second line", "third line" };

        File.WriteAllLines(_sourcePath, new[] { ignoredLine });

        using var captureLog = new CaptureLog(_destinationPath, _sourcePath, true);
        captureLog.StartCapture();

        File.AppendAllLines(_destinationPath, logLines.Take(1));
        captureLog.Flush();
        Assert.Contains(logLines.First(), File.ReadAllText(_destinationPath));

        File.AppendAllLines(_destinationPath, logLines.Skip(1));

        captureLog.StopCapture();

        // Get the stream and assert we do have the correct lines
        using var captureStream = captureLog.GetReader();
        string logLine;
        logLines.Add(ignoredLine);
        while ((logLine = captureStream.ReadLine()) != null)
        {
            if (!string.IsNullOrEmpty(logLine))
            {
                Assert.Contains(logLine, logLines);
            }
        }
    }
}
