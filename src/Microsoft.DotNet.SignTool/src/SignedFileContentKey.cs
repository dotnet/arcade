// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.SignTool
{
    /// <summary>
    /// Key that uniquely determines the signed content of a file.
    /// Two files that have the same key must have the same content once they are signed.
    /// </summary>
    internal struct SignedFileContentKey : IEquatable<SignedFileContentKey>
    {
        public readonly string StringHash;
        public readonly string FileName;

        public SignedFileContentKey(ImmutableArray<byte> contentHash, string fileName)
        {
            Debug.Assert(!contentHash.IsDefault);
            Debug.Assert(fileName != null);

            StringHash = ContentUtil.HashToString(contentHash);
            FileName = fileName;
        }

        public SignedFileContentKey(string stringHash, string fileName)
        {
            StringHash = stringHash;
            FileName = fileName;
        }

        public override bool Equals(object obj)
            => obj is SignedFileContentKey key && Equals(key);

        public override int GetHashCode()
            => Hash.Combine(FileName, StringHash.GetHashCode());

        bool IEquatable<SignedFileContentKey>.Equals(SignedFileContentKey other)
            => FileName == other.FileName && StringHash == other.StringHash;

        public static bool operator ==(SignedFileContentKey key1, SignedFileContentKey key2)
            => key1.Equals(key2);

        public static bool operator !=(SignedFileContentKey key1, SignedFileContentKey key2)
            => !(key1 == key2);
    }
}
