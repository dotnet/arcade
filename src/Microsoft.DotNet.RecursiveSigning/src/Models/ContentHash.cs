// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Strongly-typed SHA-256 content hash.
    /// </summary>
    public readonly struct ContentHash : IEquatable<ContentHash>
    {
        public ImmutableArray<byte> Bytes { get; }

        public ContentHash(ImmutableArray<byte> bytes)
        {
            if (bytes.IsDefaultOrEmpty)
            {
                throw new ArgumentException("Content hash cannot be empty", nameof(bytes));
            }

            Bytes = bytes;
        }

        public bool Equals(ContentHash other) => Bytes.SequenceEqual(other.Bytes);

        public override bool Equals(object? obj) => obj is ContentHash other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < Math.Min(4, Bytes.Length); i++)
                {
                    hash = (hash * 31) + Bytes[i];
                }
                return hash;
            }
        }

        public override string ToString()
        {
            if (Bytes.Length >= 4)
            {
                return $"{Bytes[0]:X2}{Bytes[1]:X2}{Bytes[2]:X2}{Bytes[3]:X2}...";
            }

            return "EmptyHash";
        }

        public string ToHexString() => Convert.ToHexString(Bytes.AsSpan());

        public static async Task<ContentHash> FromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            return new ContentHash(ImmutableArray.Create(hash));
        }

        public static bool operator ==(ContentHash left, ContentHash right) => left.Equals(right);

        public static bool operator !=(ContentHash left, ContentHash right) => !left.Equals(right);
    }
}
