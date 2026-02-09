// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Abstractions
{
    /// <summary>
    /// Calculates signing information for files.
    /// Phase 1: Stub implementation (always returns test certificate).
    /// </summary>
    public interface ISignatureCalculator
    {
        /// <summary>
        /// Calculate signing information for a file.
        /// </summary>
        /// <param name="metadata">File metadata.</param>
        /// <param name="configuration">Signing configuration.</param>
        /// <returns>Certificate identifier to use for signing, or null if the file should not be signed.</returns>
        ICertificateIdentifier? CalculateCertificateIdentifier(IFileMetadata metadata, SigningConfiguration configuration);
    }
}
