// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Tests
{
    /// <summary>
    /// Stub container handler for Phase 1 testing.
    /// Handles files with ".testcontainer" extension.
    /// Contains simple in-memory entries for testing nested scenarios.
    /// </summary>
    public sealed class StubContainerHandler : IContainerHandler
    {
        // Store container contents in memory
        private readonly Dictionary<string, List<(string relativePath, string content)>> _containerContents = new();

        public bool CanHandle(string filePath)
        {
            return filePath.EndsWith(".testcontainer", StringComparison.OrdinalIgnoreCase);
        }

        public async IAsyncEnumerable<ContainerEntry> ReadEntriesAsync(
            string containerPath,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!_containerContents.TryGetValue(containerPath, out var entries))
            {
                // In tests, nested containers are often extracted to random temp paths.
                // Allow lookup by file name to reuse configured contents.
                string fileName = Path.GetFileName(containerPath);
                if (!string.IsNullOrEmpty(fileName))
                {
                    var match = _containerContents.FirstOrDefault(kvp =>
                        string.Equals(Path.GetFileName(kvp.Key), fileName, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(match.Key))
                    {
                        entries = match.Value;
                        _containerContents[containerPath] = entries;
                    }
                }

                if (entries is null)
                {
                    // Create default test entries
                    entries = new List<(string, string)>
                    {
                        ("file1.txt", "Content of file 1"),
                        ("file2.txt", "Content of file 2"),
                        ("nested/file3.txt", "Content of file 3")
                    };

                    _containerContents[containerPath] = entries;
                }
            }

            foreach (var (relativePath, content) in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var bytes = Encoding.UTF8.GetBytes(content);
                var stream = new MemoryStream(bytes);

                yield return new ContainerEntry(relativePath, stream)
                {
                    ContentHash = System.Security.Cryptography.SHA256.HashData(bytes),
                    Length = bytes.Length,
                };
            }

            await Task.CompletedTask;
        }

        public Task WriteContainerAsync(
            string containerPath,
            IEnumerable<ContainerEntry> entries,
            ContainerMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            // Update container contents with new entries
            var updatedEntries = new List<(string relativePath, string content)>();

            foreach (var entry in entries)
            {
                string content;
                if (!string.IsNullOrEmpty(entry.UpdatedContentPath))
                {
                    // In the real workflow this is a temp path extracted from the container handler.
                    // Read via file APIs for simplicity in tests.
                    content = File.ReadAllText(entry.UpdatedContentPath);
                }
                else if (entry.ContentStream is not null)
                {
                    // Some tests may still provide content via stream for convenience.
                    using var reader = new StreamReader(entry.ContentStream);
                    content = reader.ReadToEnd();
                }
                else
                {
                    throw new InvalidOperationException($"Entry '{entry.RelativePath}' has no content available (both {nameof(entry.ContentStream)} and {nameof(entry.UpdatedContentPath)} are null).");
                }

                updatedEntries.Add((entry.RelativePath, content));
            }

            _containerContents[containerPath] = updatedEntries;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Helper method to set up custom container contents for testing.
        /// </summary>
        public void SetContainerContents(string containerPath, List<(string relativePath, string content)> entries)
        {
            _containerContents[containerPath] = entries;
        }

        /// <summary>
        /// Helper method to get container contents after repacking (for test verification).
        /// </summary>
        public List<(string relativePath, string content)>? GetContainerContents(string containerPath)
        {
            return _containerContents.TryGetValue(containerPath, out var entries) ? entries : null;
        }
    }
}
