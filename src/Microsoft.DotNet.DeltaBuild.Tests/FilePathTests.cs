// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Arcade.Test.Common;
using Xunit;

namespace Microsoft.DotNet.DeltaBuild.Tests;

public class FilePathTests
{
    [Fact]
    public void Create_CreatesCorrectPath_Relative()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, string>
        {
            { Path.GetFullPath("/foo/bar.txt"), "" }
        });

        var path = FilePath.Create(fileSystem, "bar.txt", "/foo");

        Assert.Equal(Path.GetFullPath("/foo/bar.txt"), path.FullPath);
        Assert.True(path.Exists);
    }

    [Fact]
    public void Create_CreatesCorrectPath_Absolute()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, string>
        {
            { Path.GetFullPath("/foo/bar.txt"), "" }
        });

        var path = FilePath.Create(fileSystem, "/foo/bar.txt", "/baz");

        Assert.Equal(Path.GetFullPath("/foo/bar.txt"), path.FullPath);
        Assert.True(path.Exists);
    }

    [Fact]
    public void Create_SingleParameter_CreatesCorrectPath()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, string>
        {
            { Path.GetFullPath("/foo/bar.txt"), "" }
        });

        var path = FilePath.Create(fileSystem, "/foo/bar.txt");

        Assert.Equal(Path.GetFullPath("/foo/bar.txt"), path.FullPath);
        Assert.True(path.Exists);
    }

    [Fact]
    public void ChangeRoot_ChangesRootCorrectly()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, string>
        {
            { Path.GetFullPath("/old/foo/bar.txt"), "" }
        });

        var path = FilePath.Create(fileSystem, "/old/foo/bar.txt");
        var newPath = path.ChangeRoot(new DirectoryInfo("/old/foo"), new DirectoryInfo("/new/baz"));

        Assert.Equal(Path.GetFullPath("/new/baz/bar.txt"), newPath.FullPath);
        Assert.False(newPath.Exists);
    }

    [Fact]
    public void ChangeRoot_ThrowsException_WhenRootDoesNotMatch()
    {
        var fileSystem = new MockFileSystem(new Dictionary<string, string>
        {
            { Path.GetFullPath("/old/foo/bar.txt"), "" }
        });

        var path = FilePath.Create(fileSystem, "/old/foo/bar.txt");

        Assert.Throws<ArgumentException>(() =>
            path.ChangeRoot(new DirectoryInfo("/wrong/foo"), new DirectoryInfo("/new/baz")));
    }

    [Fact]
    public void Equals_ReturnsTrue_ForSamePaths()
    {
        var fileSystem = new MockFileSystem();

        var path1 = FilePath.Create(fileSystem, "/foo/bar.txt");
        var path2 = FilePath.Create(fileSystem, "/foo/bar.txt");

        Assert.True(path1.Equals(path1, path2));
    }

    [Fact]
    public void GetHashCode_ReturnsSameHashCode_ForSamePaths()
    {
        var fileSystem = new MockFileSystem();

        var path1 = FilePath.Create(fileSystem, "/foo/bar.txt");
        var path2 = FilePath.Create(fileSystem, "/foo/bar.txt");

        Assert.Equal(path1.GetHashCode(path1), path1.GetHashCode(path2));
    }

    [Fact]
    public void ImplicitOperator_ReturnsCorrectString()
    {
        // Arrange
        var fileSystem = new MockFileSystem(new Dictionary<string, string>
        {
            { Path.GetFullPath("/foo/bar.txt"), "" }
        });
        var path = FilePath.Create(fileSystem, "/foo/bar.txt");

        // Act
        string pathString = path;

        // Assert
        Assert.Equal(Path.GetFullPath("/foo/bar.txt"), pathString);
    }

    [Fact]
    public void Create_NonExistentFile_ReturnsFalseForExists()
    {
        // Arrange
        string nonExistentFilePath = "/some/obscure/nonexistent/file.txt";
        string rootPath = "/some/obscure";

        // Act
        var path = FilePath.Create(nonExistentFilePath, rootPath);

        // Assert
        Assert.False(path.Exists);
    }

}
