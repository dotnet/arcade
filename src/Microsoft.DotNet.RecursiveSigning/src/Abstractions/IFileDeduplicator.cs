// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Abstractions
{
    /// <summary>
    /// Manages file deduplication based on content.
    /// </summary>
    public interface IFileDeduplicator
    {
        /// <summary>
        /// Register a file's metadata.
        /// </summary>
        /// <param name="contentKey">Content key.</param>
        /// <param name="filePathOnDisk">Full path to the file on disk.</param>
        /// <exception cref="System.InvalidOperationException">Thrown when the content key has already been registered.</exception>
        void RegisterFile(FileContentKey contentKey, string filePathOnDisk);

        bool TryGetRegisteredFile(FileContentKey contentKey, out string? originalPath);

        /// <summary>
        /// Try to get the signed version of a file by its content key.
        /// </summary>
        /// <param name="key">File content key.</param>
        /// <param name="signedPath">Path to signed version if found.</param>
        /// <returns>True if signed version found.</returns>
        bool TryGetSignedVersion(FileContentKey key, out string signedPath);

        /// <summary>
        /// Register that a file has been signed.
        /// </summary>
        /// <param name="key">File content key.</param>
        /// <param name="signedPath">Path to signed version.</param>
        void RegisterSignedFile(FileContentKey key, string signedPath);
    }
}
