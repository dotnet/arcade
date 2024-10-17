// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using Microsoft.DotNet.XHarness.Common.Logging;
using Microsoft.DotNet.XHarness.iOS.Shared.Listeners;
using Moq;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Listeners;

public class SimpleFileListenerTest : IDisposable
{
    private readonly string _path;
    private readonly Mock<IFileBackedLog> _testLog;
    private readonly Mock<ILog> _log;

    public SimpleFileListenerTest()
    {
        _path = Path.GetTempFileName();
        _testLog = new Mock<IFileBackedLog>();
        _log = new Mock<ILog>();

        try
        {
            File.Delete(_path);
        }
        finally
        {
        }
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            try
            {
                File.Delete(_path);
            }
            finally
            {
            }
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ConstructorNullPathTest() => Assert.Throws<ArgumentNullException>(() => new SimpleFileListener(null, _log.Object, _testLog.Object, false));

    [Theory]
    [InlineData("Tests run: ", false)]
    [InlineData("<!-- the end -->", true)]
    public void FileContentIsCopied(string endLine, bool isXml)
    {
        var lines = new[] { "first line", "second line", "last line" };

        // Create a listener, set the writer and ensure that what we write in the file is present in the final path
        using (var sourceWriter = new StreamWriter(_path))
        {
            using var listener = new SimpleFileListener(_path, _log.Object, _testLog.Object, isXml);
            listener.InitializeAndGetPort();
            listener.StartAsync();

            // Write a number of lines and ensure that those are called in the mock
            sourceWriter.WriteLine("[Runner executing:");
            foreach (var line in lines)
            {
                sourceWriter.WriteLine(line);
            }

            sourceWriter.WriteLine(endLine);
            sourceWriter.Flush();
        }

        Thread.Sleep(200);

        // Verify that the expected lines were added
        foreach (var line in lines)
        {
            _testLog.Verify(l => l.WriteLine(It.Is<string>(ll => ll.Trim() == line.Trim())), Times.AtLeastOnce);
        }

        _log.Verify(l => l.WriteLine(It.Is<string>(ll => ll == "Tests have finished executing")), Times.AtLeastOnce);
    }
}
