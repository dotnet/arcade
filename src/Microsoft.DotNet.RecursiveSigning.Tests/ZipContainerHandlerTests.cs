// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Arcade.Common;
using Microsoft.Arcade.Test.Common;
using Microsoft.DotNet.RecursiveSigning.Implementation;
using Microsoft.DotNet.RecursiveSigning.Models;
using Xunit;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    public sealed class ZipContainerHandlerTests
    {
        [Theory]
        [InlineData("a.zip", true)]
        [InlineData("a.nupkg", true)]
        [InlineData("a.vsix", true)]
        [InlineData("a.ZIP", true)]
        [InlineData("a.txt", false)]
        [InlineData("", false)]
        public void CanHandle_UsesExtension(string path, bool expected)
        {
            var handler = new ZipContainerHandler();
            handler.CanHandle(path).Should().Be(expected);
        }

        [Fact]
        public async Task ReadEntriesAsync_ReturnsExpectedEntriesWithHashAndLength()
        {
            var mockFileSystem = new MockFileSystem(files: new Dictionary<string, string>(), directories: new[] { "/test" });
            var handler = new ZipContainerHandler(mockFileSystem);

            string containerPath = "/test/test.zip";
            mockFileSystem.WriteAllBytes(containerPath, CreateZipBytes(new Dictionary<string, byte[]>
            {
                ["a.txt"] = Encoding.UTF8.GetBytes("hello"),
                ["nested/b.bin"] = new byte[] { 1, 2, 3 },
                ["dir/"] = Array.Empty<byte>(),
            }));

            var entries = new List<ContainerEntry>();
            await foreach (ContainerEntry entry in handler.ReadEntriesAsync(containerPath))
            {
                entries.Add(entry);
            }

            entries.Select(e => e.RelativePath).Should().BeEquivalentTo(new[] { "a.txt", "nested/b.bin" });

            var a = entries.Single(e => e.RelativePath == "a.txt");
            a.Length.Should().Be(5);
            a.ContentHash.Should().NotBeNull();
            a.ContentHash!.Should().Equal(SHA256.HashData(Encoding.UTF8.GetBytes("hello")));

            var b = entries.Single(e => e.RelativePath == "nested/b.bin");
            b.Length.Should().Be(3);
            b.ContentHash!.Should().Equal(SHA256.HashData(new byte[] { 1, 2, 3 }));

            foreach (ContainerEntry entry in entries)
            {
                entry.Dispose();
            }
        }

        [Fact]
        public async Task WriteContainerAsync_ReplacesUpdatedEntries_AndDoesNotDisposeStreams()
        {
            var mockFileSystem = new MockFileSystem(files: new Dictionary<string, string>(), directories: new[] { "/test" });
            var handler = new ZipContainerHandler(mockFileSystem);

            string containerPath = "/test/test.zip";
            mockFileSystem.WriteAllBytes(containerPath, CreateZipBytes(new Dictionary<string, byte[]>
            {
                ["a.txt"] = Encoding.UTF8.GetBytes("old"),
                ["b.txt"] = Encoding.UTF8.GetBytes("keep"),
            }));

            string updatedPath = "/test/updated.txt";
            mockFileSystem.WriteAllBytes(updatedPath, Encoding.UTF8.GetBytes("new"));

            var entries = new[]
            {
                new ContainerEntry("a.txt", contentStream: null) { UpdatedContentPath = updatedPath },
            };

            await handler.WriteContainerAsync(containerPath, entries, new ContainerMetadata());

            byte[] updatedZip = mockFileSystem.ReadAllBytes(containerPath);
            ReadZipEntryText(updatedZip, "a.txt").Should().Be("new");
            ReadZipEntryText(updatedZip, "b.txt").Should().Be("keep");
        }

        private static byte[] CreateZipBytes(IReadOnlyDictionary<string, byte[]> entries)
        {
            using var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var kvp in entries)
                {
                    var entry = archive.CreateEntry(kvp.Key, CompressionLevel.Optimal);
                    using Stream entryStream = entry.Open();
                    byte[] bytes = kvp.Value;
                    entryStream.Write(bytes, 0, bytes.Length);
                }
            }

            return ms.ToArray();
        }

        private static string ReadZipEntryText(byte[] zipBytes, string entryName)
        {
            using var ms = new MemoryStream(zipBytes, writable: false);
            using var archive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);
            var entry = archive.GetEntry(entryName);
            entry.Should().NotBeNull();

            using Stream entryStream = entry!.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            return reader.ReadToEnd();
        }

    }
}
