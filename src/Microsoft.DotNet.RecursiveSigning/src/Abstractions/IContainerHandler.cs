// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Abstractions
{
    /// <summary>
    /// Handler for reading and writing container (archive) files.
    /// </summary>
    public interface IContainerHandler
    {
        /// <summary>
        /// Check if this handler can process the given file.
        /// </summary>
        /// <param name="filePath">File path to check.</param>
        /// <returns>True if this handler supports the file format.</returns>
        bool CanHandle(string filePath);

        /// <summary>
        /// Read entries from a container.
        /// </summary>
        /// <param name="containerPath">Path to container file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// Async enumerable of container entries.
        /// The caller owns each returned <see cref="ContainerEntry"/> (and its <see cref="ContainerEntry.ContentStream"/>)
        /// and must dispose it when finished reading the entry.
        /// </returns>
        IAsyncEnumerable<ContainerEntry> ReadEntriesAsync(
            string containerPath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Write updated container with signed contents.
        /// </summary>
        /// <param name="containerPath">Path to container file.</param>
        /// <param name="entriesToUpdate">Entries to update. Not all entries in the container may be updated.</param>
        /// <param name="metadata">Container metadata to preserve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// The caller retains ownership of <paramref name="entriesToUpdate"/> and their streams.
        /// Implementations must not dispose entry streams.
        /// </remarks>
        Task WriteContainerAsync(
            string containerPath,
            IEnumerable<ContainerEntry> entriesToUpdate,
            ContainerMetadata metadata,
            CancellationToken cancellationToken = default);
    }
}
