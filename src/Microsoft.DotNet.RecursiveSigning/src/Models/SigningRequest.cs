// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Request to the signing orchestrator.
    /// </summary>
    public sealed class SigningRequest
    {
        /// <summary>
        /// Input files to sign (top-level artifacts).
        /// </summary>
        public IReadOnlyList<FileInfo> InputFiles { get; }

        /// <summary>
        /// Signing configuration.
        /// </summary>
        public SigningConfiguration Configuration { get; }

        /// <summary>
        /// Options for signing process.
        /// </summary>
        public SigningOptions Options { get; }

        public SigningRequest(
            IReadOnlyList<FileInfo> inputFiles,
            SigningConfiguration configuration,
            SigningOptions options)
        {
            InputFiles = inputFiles ?? throw new ArgumentNullException(nameof(inputFiles));
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }
    }
}
