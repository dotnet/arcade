// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Abstractions
{
    /// <summary>
    /// Analyzes files and extracts metadata.
    /// Phase 1: Stub implementation (only hash + filename).
    /// </summary>
    public interface IFileAnalyzer
    {
        /// <summary>
        /// Analyze a file and extract metadata.
        /// </summary>
        /// <param name="filePath">Path to file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Intrinsic file metadata.</returns>
        Task<IFileMetadata> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Analyze a stream and extract metadata without writing to disk.
        /// </summary>
        /// <param name="contentStream">Stream containing file content.</param>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Intrinsic file metadata.</returns>
        Task<IFileMetadata> AnalyzeAsync(Stream contentStream, string fileName, CancellationToken cancellationToken = default);
    }
}
