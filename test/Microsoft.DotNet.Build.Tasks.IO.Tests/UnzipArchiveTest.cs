// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Compression;
using Xunit;

using ZipArchiveStream = System.IO.Compression.ZipArchive;

namespace Microsoft.DotNet.Build.Tasks.IO.Tests
{
    public class UnzipArchiveTest : IDisposable
    {
        private readonly string _tempDir;

        public UnzipArchiveTest()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
        }

        [Fact]
        public void UnzipsFile()
        {
            var files = new[]
            {
                "a.txt",
                "dir/b.txt",
            };

            var dest = CreateZip(files);
            var outDir = Path.Combine(_tempDir, "out");

            var task = new UnzipArchive
            {
                File = dest,
                DestinationFolder = outDir,
                BuildEngine = new MockEngine(),
            };

            Assert.True(task.Execute(), "The task failed but should have passed.");
            Assert.True(Directory.Exists(outDir), outDir + " does not exist");
            Assert.Equal(files.Length, task.OutputFiles.Length);

            Assert.All(task.OutputFiles,
                f => Assert.True(Path.IsPathRooted(f.ItemSpec), $"Entry {f} should be a fullpath rooted"));

            foreach (var file in files)
            {
                var outFile = Path.Combine(outDir, file);
                Assert.True(File.Exists(outFile), outFile + " does not exist");
            }
        }

        [Fact]
        public void UnzipsSubdirectories()
        {
            var files = new[]
            {
                "a/b/c/d.dll",
                "e/f/j/k/l.json",
                "e/f/m/n/o.json"
            };

            var dest = CreateZip(files);
            var outDir = Path.Combine(_tempDir, "out");

            var task = new UnzipArchive
            {
                File = dest,
                DestinationFolder = outDir,
                BuildEngine = new MockEngine(),
            };

            Assert.True(task.Execute(), "The task failed but should have passed.");
            Assert.True(Directory.Exists(outDir), outDir + " does not exist");
            Assert.Equal(files.Length, task.OutputFiles.Length);

            Assert.All(task.OutputFiles,
                f => Assert.True(Path.IsPathRooted(f.ItemSpec), $"Entry {f} should be a fullpath rooted"));

            foreach (var file in files)
            {
                var outFile = Path.Combine(outDir, file);
                Assert.True(File.Exists(outFile), outFile + " does not exist");
            }
        }

        [Fact]
        public void Overwrites()
        {
            var files = new[]
            {
                "a.txt",
                "dir/b.txt"
            };

            var dest = CreateZip(files);
            var outDir = Path.Combine(_tempDir, "out");

            var task = new UnzipArchive
            {
                File = dest,
                DestinationFolder = outDir,
                BuildEngine = new MockEngine(),
                Overwrite = true
            };

            Directory.CreateDirectory(outDir);

            // Create a.txt before trying to unzip
            var path = Path.Combine(outDir, "a.txt");
            File.WriteAllText(path, "contents!");
            Assert.True(task.Execute(), "The task failed but should have passed.");
            Assert.Empty(File.ReadAllText(path));
        }

        [Fact]
        public void DoesNotOverwrite()
        {
            var files = new[]
            {
                "a.txt",
                "dir/b.txt"
            };

            var dest = CreateZip(files);
            var outDir = Path.Combine(_tempDir, "out");

            var task = new UnzipArchive
            {
                File = dest,
                DestinationFolder = outDir,
                BuildEngine = new MockEngine(),
                Overwrite = false
            };

            Directory.CreateDirectory(outDir);

            // Create a.txt before trying to unzip
            var path = Path.Combine(outDir, "a.txt");
            var contents = "contents!";
            File.WriteAllText(path, contents);

            Assert.Throws<IOException>(() => task.Execute());

            Assert.Equal(contents, File.ReadAllText(path));
        }

        [Fact]
        public void ItNormalizesBacklashesInPath()
        {
            var files = new[]
            {
                @"dir\b.txt"
            };

            var dest = CreateZip(files);
            var outDir = Path.Combine(_tempDir, "out");

            var engine = new MockEngine();
            var task = new UnzipArchive
            {
                File = dest,
                DestinationFolder = outDir,
                BuildEngine = engine,
                Overwrite = false
            };

            Assert.True(task.Execute(), "The task failed but should have passed.");
            Assert.True(File.Exists(Path.Combine(outDir, "dir", "b.txt")), "File should exist.");
        }

        private string CreateZip(string[] files)
        {
            var dest = Path.Combine(_tempDir, "test.zip");

            using (var fileStream = new FileStream(dest, FileMode.Create))
            using (var zipStream = new ZipArchiveStream(fileStream, ZipArchiveMode.Create))
            {
                foreach (var file in files)
                {
                    zipStream.CreateEntry(file);
                }
            }

            return dest;
        }

        public void Dispose()
        {
            TestHelpers.DeleteDirectory(_tempDir);
        }
    }
}
