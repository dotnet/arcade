// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Uniquely identifies a file by its content hash and filename.
    /// Used for deduplication across containers.
    /// </summary>
    public readonly struct FileContentKey : IEquatable<FileContentKey>
    {
        /// <summary>
        /// SHA-256 hash of the file content.
        /// </summary>
        public ContentHash ContentHash { get; }

        /// <summary>
        /// File name (without path).
        /// </summary>
        public string FileName { get; }

        public FileContentKey(ContentHash contentHash, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
            }

            ContentHash = contentHash;
            FileName = fileName;
        }

        public bool Equals(FileContentKey other)
        {
            return ContentHash.Equals(other.ContentHash) &&
                   string.Equals(FileName, other.FileName, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object? obj)
        {
            return obj is FileContentKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                // Use first 4 bytes of hash for GetHashCode
                var bytes = ContentHash.Bytes;
                for (int i = 0; i < Math.Min(4, bytes.Length); i++)
                {
                    hash = hash * 31 + bytes[i];
                }
                hash = hash * 31 + StringComparer.OrdinalIgnoreCase.GetHashCode(FileName);
                return hash;
            }
        }

        public override string ToString()
        {
            string hashStr = ContentHash.ToString();
            return $"{FileName} ({hashStr})";
        }

        public static bool operator ==(FileContentKey left, FileContentKey right) => left.Equals(right);
        public static bool operator !=(FileContentKey left, FileContentKey right) => !left.Equals(right);
    }
}
