// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Abstractions
{
    /// <summary>
    /// Provider for signing operations.
    /// Phase 1: Fake implementation for testing.
    /// </summary>
    public interface ISigningProvider
    {
        /// <summary>
        /// Sign a batch of files.
        /// </summary>
        /// <param name="files">Files to sign with their signing information.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if all files signed successfully.</returns>
        Task<bool> SignFilesAsync(
            IReadOnlyList<(FileNode node, string outputPath)> files,
            CancellationToken cancellationToken = default);
    }
}
