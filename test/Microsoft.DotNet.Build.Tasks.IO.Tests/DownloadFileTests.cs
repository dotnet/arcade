// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.Build.Tasks.IO.Tests
{
    public class DownloadFileTests
    {
        private readonly ITestOutputHelper _output;

        public DownloadFileTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ItDownloadAFile()
        {
            var expectedPath = Path.Combine(AppContext.BaseDirectory, Path.GetRandomFileName());
            var task = new DownloadFile
            {
                Uri = "http://example.org/index.html",
                OutputPath = expectedPath,
                BuildEngine = new MockEngine(_output),
            };

            if (File.Exists(expectedPath))
            {
                File.Delete(expectedPath);
            }

            Assert.True(await task.ExecuteAsync(), "Task should pass");
            Assert.True(File.Exists(expectedPath), "The file should exist");
        }

        [Fact]
        public async Task ItFailsForFilesThatDoNotExist()
        {
            var engine = new MockEngine(_output) { ContinueOnError = true };
            var task = new DownloadFile
            {
                Uri = "http://localhost/this/file/does/not/exist",
                OutputPath = Path.Combine(AppContext.BaseDirectory, "dummy.txt"),
                BuildEngine = engine,
            };

            Assert.False(await task.ExecuteAsync(), "Task should fail");
            Assert.NotEmpty(engine.Errors);
        }

        [Fact]
        public async Task ItFailsIfFileAlreadyExists()
        {
            var engine = new MockEngine(_output) { ContinueOnError = true };
            var expectedPath = Path.Combine(AppContext.BaseDirectory, Path.GetRandomFileName());
            File.WriteAllText(expectedPath, "");
            var task = new DownloadFile
            {
                Uri = "http://localhost/this/file/does/not/exist",
                OutputPath = expectedPath,
                BuildEngine = engine,
            };

            Assert.False(await task.ExecuteAsync(), "Task should fail");
            Assert.NotEmpty(engine.Errors);
        }
    }
}
