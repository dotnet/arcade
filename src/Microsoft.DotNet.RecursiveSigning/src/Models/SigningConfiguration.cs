// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Minimal signing configuration for Phase 1.
    /// </summary>
    public sealed class SigningConfiguration
    {
        /// <summary>
        /// Temporary directory for unpacking containers and other intermediate files.
        /// </summary>
        public string TempDirectory { get; }

        /// <summary>
        /// Optional output directory for root input artifacts.
        /// When set, final signed root files are copied here while working files continue to be updated in place.
        /// </summary>
        public string? OutputDirectory { get; }

        public SigningConfiguration(string tempDirectory, string? outputDirectory = null)
        {
            TempDirectory = tempDirectory ?? throw new ArgumentNullException(nameof(tempDirectory));
            OutputDirectory = string.IsNullOrWhiteSpace(outputDirectory) ? null : outputDirectory;
        }
    }
}
