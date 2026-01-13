// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    /// <summary>
    /// Default implementation of IFileDeduplicator.
    /// Manages file deduplication based on content hash + filename.
    /// Tracks which files have the same content and reuses signed versions.
    /// </summary>
    public sealed class DefaultFileDeduplicator : IFileDeduplicator
    {
        private readonly ConcurrentDictionary<FileContentKey, string> _originalPathsByContentKey = new();
        private readonly ConcurrentDictionary<FileContentKey, string> _signedVersions = new();

        public void RegisterFile(FileContentKey contentKey, string filePathOnDisk)
        {
            if (string.IsNullOrWhiteSpace(filePathOnDisk))
            {
                throw new ArgumentException("File path cannot be null or empty", nameof(filePathOnDisk));
            }

            // Track the first path observed for this content key.
            if (!_originalPathsByContentKey.TryAdd(contentKey, filePathOnDisk))
            {
                throw new InvalidOperationException($"File with content key '{contentKey}' has already been registered.");
            }
        }

        public bool TryGetRegisteredFile(FileContentKey contentKey, out string? originalPath)
        {
            return _originalPathsByContentKey.TryGetValue(contentKey, out originalPath);
        }

        public bool TryGetSignedVersion(FileContentKey key, out string signedPath)
        {
            return _signedVersions.TryGetValue(key, out signedPath!);
        }

        public void RegisterSignedFile(FileContentKey key, string signedPath)
        {
            if (string.IsNullOrWhiteSpace(signedPath))
            {
                throw new ArgumentException("Signed path cannot be null or empty", nameof(signedPath));
            }

            _signedVersions[key] = signedPath;
        }
    }
}
