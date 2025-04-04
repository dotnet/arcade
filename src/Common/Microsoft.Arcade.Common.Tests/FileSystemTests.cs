// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit;

namespace Microsoft.Arcade.Common.Tests
{
    public class FileSystemTests
    {
        [WindowsOnlyTheory]
        [InlineData(@"C:\Projects\MyApp", @"C:\Projects\MyApp\SubFolder", @"SubFolder")]
        [InlineData(@"C:\Projects\MyApp\", @"C:\Projects\MyApp\SubFolder", @"SubFolder")]
        [InlineData(@"C:\Projects\MyApp", @"C:\Projects\MyApp\SubFolder\File.txt", @"SubFolder\File.txt")]
        [InlineData(@"C:\Projects\MyApp\", @"C:\Projects\MyApp\SubFolder\File.txt", @"SubFolder\File.txt")]
        public void GetRelativePath_ReturnsExpectedResult_Windows(string basePath, string targetPath, string expected)
        {
            FileSystem fileSystem = new();
            string result = fileSystem.GetRelativePath(basePath, targetPath);
            Assert.Equal(expected, result);
        }

        [UnixOnlyTheory]
        [InlineData(@"/home/user/projects", @"/home/user/projects/subfolder", @"subfolder")]
        [InlineData(@"/home/user/projects/", @"/home/user/projects/subfolder", @"subfolder")]
        [InlineData(@"/home/user/projects", @"/home/user/projects/subfolder/file.txt", @"subfolder/file.txt")]
        [InlineData(@"/home/user/projects/", @"/home/user/projects/subfolder/file.txt", @"subfolder/file.txt")]
        public void GetRelativePath_ReturnsExpectedResult_Unix(string basePath, string targetPath, string expected)
        {
            FileSystem fileSystem = new();
            string result = fileSystem.GetRelativePath(basePath, targetPath);
            Assert.Equal(expected, result);
        }

        [WindowsOnlyFact]
        public void GetRelativePath_Throws_Windows()
        {
            FileSystem fileSystem = new FileSystem();
            Assert.Throws<ArgumentException>(() => fileSystem.GetRelativePath(@"C:\Projects\MyApp", @"C:\Projects\AnotherApp"));
     
        }

        [UnixOnlyFact]
        public void GetRelativePath_Throws_Unix()
        {
            FileSystem fileSystem = new FileSystem();
            Assert.Throws<ArgumentException>(() => fileSystem.GetRelativePath(@"/home/user/projects", @"/home/user/anotherapp"));
        }
    }
}
