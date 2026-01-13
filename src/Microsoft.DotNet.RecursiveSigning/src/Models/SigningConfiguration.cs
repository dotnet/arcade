// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        public SigningConfiguration(string tempDirectory)
        {
            TempDirectory = tempDirectory ?? throw new System.ArgumentNullException(nameof(tempDirectory));
        }
    }
}
