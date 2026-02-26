// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Arcade.Common;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    public sealed class ZipContainerHandler : IContainerHandler
    {
        private static readonly string[] SupportedExtensions = new[] { ".zip", ".nupkg", ".vsix" };
        private readonly IFileSystem _fileSystem;

        public ZipContainerHandler(IFileSystem? fileSystem = null)
        {
            _fileSystem = fileSystem ?? new FileSystem();
        }

        public bool CanHandle(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            string extension = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }

            foreach (string supportedExtension in SupportedExtensions)
            {
                if (string.Equals(extension, supportedExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public async IAsyncEnumerable<ContainerEntry> ReadEntriesAsync(
            string containerPath,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(containerPath))
            {
                throw new ArgumentException("Container path cannot be null or empty", nameof(containerPath));
            }

            await using var containerStream = _fileSystem.GetFileStream(containerPath, FileMode.Open, FileAccess.Read);
            using var archive = new ZipArchive(containerStream, ZipArchiveMode.Read, leaveOpen: false);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsDirectoryEntry(entry))
                {
                    continue;
                }

                var buffer = new MemoryStream(capacity: entry.Length > int.MaxValue ? 0 : (int)entry.Length);
                using (Stream entryStream = entry.Open())
                {
                    await entryStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                }

                buffer.Position = 0;

                byte[] hash = SHA256.HashData(buffer.ToArray());
                buffer.Position = 0;

                yield return new ContainerEntry(entry.FullName, buffer)
                {
                    Length = buffer.Length,
                    ContentHash = hash,
                };
            }
        }

        public async Task WriteContainerAsync(
            string containerPath,
            IEnumerable<ContainerEntry> entries,
            ContainerMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(containerPath))
            {
                throw new ArgumentException("Container path cannot be null or empty", nameof(containerPath));
            }

            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            await using var containerStream = _fileSystem.GetFileStream(containerPath, FileMode.Open, FileAccess.ReadWrite);
            using var archive = new ZipArchive(containerStream, ZipArchiveMode.Update, leaveOpen: false);

            foreach (ContainerEntry entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry == null || string.IsNullOrEmpty(entry.UpdatedContentPath))
                {
                    continue;
                }

                ZipArchiveEntry? archiveEntry = archive.GetEntry(entry.RelativePath);
                if (archiveEntry == null)
                {
                    // For now, only overwrite existing entries.
                    continue;
                }

                await using Stream entryStream = archiveEntry.Open();

                await using Stream signedStream = _fileSystem.GetFileStream(entry.UpdatedContentPath, FileMode.Open, FileAccess.Read);
                await signedStream.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);
                entryStream.SetLength(signedStream.Length);
            }
        }

        private static bool IsDirectoryEntry(ZipArchiveEntry entry)
        {
            // Directory entries typically have a trailing slash and length 0.
            return entry.FullName.EndsWith("/", StringComparison.Ordinal);
        }
    }
}
