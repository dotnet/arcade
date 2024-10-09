// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.DotNet.XHarness.iOS.Shared.Logging;
using Xunit;

namespace Microsoft.DotNet.XHarness.iOS.Shared.Tests.Logging;

public class LogsTest : IDisposable
{
    private readonly string _directory;
    private string _fileName;
    private readonly string _description;

    public LogsTest()
    {
        _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _fileName = "test-file.txt";
        _description = "My description";

        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ConstructorTest()
    {
        using (var logs = new Logs(_directory))
        {
            Assert.Equal(_directory, logs.Directory);
        }
    }

    [Fact]
    public void ConstructorNullDirTest() => Assert.Throws<ArgumentNullException>(() => new Logs(null));

    [Fact]
    public void CreateFileTest()
    {
        using (var logs = new Logs(_directory))
        {
            var file = logs.CreateFile(_fileName, _description);
            Assert.True(File.Exists(file), "exists");
            Assert.Equal(_fileName, Path.GetFileName(file));
            Assert.Single(logs);
        }
    }

    [Fact]
    public void CreateFileNullPathTest()
    {
        using (var logs = new Logs(_directory))
        {
            _fileName = null;
            var description = "My description";
            Assert.Throws<ArgumentNullException>(() => logs.CreateFile(_fileName, description));
        }
    }

    [Fact]
    public void CreateFileNullDescriptionTest()
    {
        using (var logs = new Logs(_directory))
        {
            string description = null;
            logs.CreateFile(_fileName, description);
            Assert.Single(logs);
        }
    }

    [Fact]
    public void AddFileTest()
    {
        var fullPath = Path.Combine(_directory, _fileName);
        File.WriteAllText(fullPath, "foo");

        using (var logs = new Logs(_directory))
        {
            var fileLog = logs.AddFile(fullPath, _description);
            Assert.Equal(fullPath, fileLog.FullPath); // path && fullPath are the same
            Assert.Equal(Path.Combine(_directory, _fileName), fileLog.FullPath);
            Assert.Equal(_description, fileLog.Description);
        }
    }

    [Fact]
    public void AddFileNotInDirTest()
    {
        var dir1 = Path.Combine(_directory, "dir1");
        var dir2 = Path.Combine(_directory, "dir2");

        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        var filePath = Path.Combine(dir1, "test-file.txt");
        File.WriteAllText(filePath, "Hello world!");

        using (var logs = new Logs(dir2))
        {
            var newPath = Path.Combine(dir2, Path.GetFileNameWithoutExtension(_fileName));
            var fileLog = logs.AddFile(filePath, _description);
            Assert.StartsWith(newPath, fileLog.FullPath); // assert new path
            Assert.True(File.Exists(fileLog.FullPath), "copy");
        }
    }

    [Fact]
    public void AddFilePathNullTest()
    {
        using (var logs = new Logs(_directory))
        {
            Assert.Throws<ArgumentNullException>(() => logs.AddFile(null, _description));
        }
    }

    [Fact]
    public void AddFileDescriptionNull()
    {
        var fullPath = Path.Combine(_directory, _fileName);
        File.WriteAllText(fullPath, "foo");
        using (var logs = new Logs(_directory))
        {
            logs.Create(fullPath, null);
            Assert.Single(logs);
        }
    }
}
