// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

using ZipArchiveStream = System.IO.Compression.ZipArchive;

namespace Microsoft.DotNet.Build.Tasks.IO.Tests
{
    public class ZipArchiveTest : IDisposable
    {
        private readonly string _tempDir;

        public ZipArchiveTest()
        {
            _tempDir = Path.Combine(AppContext.BaseDirectory, Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        [Fact]
        public void ZipsLinkItems()
        {
            var inputFile = Path.Combine(_tempDir, "..", Guid.NewGuid().ToString());
            var dest = Path.Combine(_tempDir, "test.zip");
            var linkItem = new TaskItem(inputFile);
            linkItem.SetMetadata("Link", "temp/temp/temp/file.txt");
            try
            {
                File.WriteAllText(inputFile, "");
                var task = new ZipArchive
                {
                    SourceFiles = new[] { linkItem },
                    BaseDirectory = Path.Combine(_tempDir, "temp"),
                    OutputPath = dest,
                    BuildEngine = new TestsUtil.MockEngine(),
                };
                Assert.True(task.Execute());

                using (var fileStream = new FileStream(dest, FileMode.Open))
                using (var zipStream = new ZipArchiveStream(fileStream))
                {
                    var entry = Assert.Single(zipStream.Entries);
                    Assert.Equal("temp/temp/temp/file.txt", entry.FullName);
                }
            }
            finally
            {
                File.Delete(inputFile);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CreatesZipFromDirectory(bool includeBaseDirectory)
        {
            var files = new[]
            {
                "a.txt",
                "dir/b.txt",
                @"dir\c.txt",
            };
            CreateItems(files);

            var dest = Path.Combine(AppContext.BaseDirectory, Path.GetRandomFileName() + ".zip");
            Assert.False(File.Exists(dest));

            var task = new ZipArchive
            {
                SourceDirectory = _tempDir,
                IncludeSourceDirectory = includeBaseDirectory,
                OutputPath = dest,
                Overwrite = true,
                BuildEngine = new TestsUtil.MockEngine(),
            };

            Assert.True(task.Execute());
            Assert.True(File.Exists(dest));

            var entryPrefix = includeBaseDirectory ? Path.GetFileName(_tempDir) + "/" : null;

            using (var fileStream = new FileStream(dest, FileMode.Open))
            using (var zipStream = new ZipArchiveStream(fileStream))
            {
                Assert.Equal(files.Length, zipStream.Entries.Count);
                Assert.Collection(zipStream.Entries.OrderBy(d => d.FullName),
                    a => Assert.Equal(entryPrefix + "a.txt", a.FullName),
                    b => Assert.Equal(entryPrefix + "dir/b.txt", b.FullName),
                    c => Assert.Equal(entryPrefix + "dir/c.txt", c.FullName));
            }
        }

        [Fact]
        public void CreatesZipFromFiles()
        {
            var files = new[]
            {
                "a.txt",
                "dir/b.txt",
                @"dir\c.txt",
            };

            var dest = Path.Combine(_tempDir, "test.zip");
            Assert.False(File.Exists(dest));

            var task = new ZipArchive
            {
                SourceFiles = CreateItems(files),
                BaseDirectory = _tempDir,
                OutputPath = dest,
                Overwrite = true,
                BuildEngine = new TestsUtil.MockEngine(),
            };

            Assert.True(task.Execute());
            Assert.True(File.Exists(dest));

            using (var fileStream = new FileStream(dest, FileMode.Open))
            using (var zipStream = new ZipArchiveStream(fileStream))
            {
                Assert.Equal(files.Length, zipStream.Entries.Count);
                Assert.Collection(zipStream.Entries,
                    a => Assert.Equal("a.txt", a.FullName),
                    b => Assert.Equal("dir/b.txt", b.FullName),
                    c => Assert.Equal("dir/c.txt", c.FullName));
            }
        }

        [Fact]
        public void FailsIfFileExists()
        {
            var files = new[]
           {
                "test.txt",
            };

            var dest = Path.Combine(_tempDir, "test.zip");
            File.WriteAllText(dest, "Original");

            var task = new ZipArchive
            {
                SourceFiles = CreateItems(files),
                BaseDirectory = _tempDir,
                OutputPath = dest,
                Overwrite = false,
                BuildEngine = new TestsUtil.MockEngine { ContinueOnError = true },
            };

            Assert.False(task.Execute(), "Task should fail");
            Assert.Equal("Original", File.ReadAllText(dest));
        }

        [Fact]
        public void FailsIfMixingParameterSets()
        {
            var task = new ZipArchive
            {
                SourceFiles = new[] { new TaskItem() },
                BaseDirectory = _tempDir,
                SourceDirectory = _tempDir,
                OutputPath = Path.Combine(_tempDir, "test.zip"),
                Overwrite = false,
                BuildEngine = new TestsUtil.MockEngine { ContinueOnError = true },
            };

            Assert.False(task.Execute(), "Task should fail");
        }

        [Fact]
        public void OverwriteReplacesEntireZip()
        {
            var files1 = new[]
            {
                "a.txt",
                "dir/b.txt",
                @"dir\c.txt",
            };

            var files2 = new[]
            {
                "test.txt",
            };

            var dest = Path.Combine(_tempDir, "test.zip");
            Assert.False(File.Exists(dest));

            var task = new ZipArchive
            {
                SourceFiles = CreateItems(files1),
                BaseDirectory = _tempDir,
                OutputPath = dest,
                BuildEngine = new TestsUtil.MockEngine(),
            };

            Assert.True(task.Execute());
            Assert.True(File.Exists(dest));

            task = new ZipArchive
            {
                SourceFiles = CreateItems(files2),
                BaseDirectory = _tempDir,
                OutputPath = dest,
                Overwrite = true,
                BuildEngine = new TestsUtil.MockEngine(),
            };

            Assert.True(task.Execute());
            Assert.True(File.Exists(dest));

            using (var fileStream = File.OpenRead(dest))
            using (var zipStream = new ZipArchiveStream(fileStream))
            {
                var entry = Assert.Single(zipStream.Entries);
                Assert.Equal("test.txt", entry.FullName);
            }
        }

        [Fact]
        public void FailsForEmptyFileName()
        {
            var inputFile = Path.Combine(_tempDir, "..", Guid.NewGuid().ToString());
            var dest = Path.Combine(_tempDir, "test.zip");
            var linkItem = new TaskItem(inputFile);
            linkItem.SetMetadata("Link", "temp/");
            try
            {
                File.WriteAllText(inputFile, "");
                var mock = new TestsUtil.MockEngine { ContinueOnError = true };
                var task = new ZipArchive
                {
                    SourceFiles = new[] { linkItem },
                    BaseDirectory = Path.Combine(_tempDir, "temp"),
                    OutputPath = dest,
                    BuildEngine = mock,
                };

                Assert.False(task.Execute(), "Task should fail");
                Assert.NotEmpty(mock.Errors);

                using (var fileStream = new FileStream(dest, FileMode.Open))
                using (var zipStream = new ZipArchiveStream(fileStream))
                {
                    Assert.Empty(zipStream.Entries);
                }
            }
            finally
            {
                File.Delete(inputFile);
            }
        }

        private ITaskItem[] CreateItems(string[] files)
        {
            var items = new ITaskItem[files.Length];
            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var path = Path.Combine(_tempDir, file);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path.Replace('\\', '/'), "");
                // intentionally allow item spec to contain \ and /
                // this tests that MSBuild normalizes before we create zip entries
                items[i] = new TaskItem(path);
            }
            return items;
        }

        public void Dispose()
        {
            TestsUtil.TestHelpers.DeleteDirectory(_tempDir);
        }
    }
}
