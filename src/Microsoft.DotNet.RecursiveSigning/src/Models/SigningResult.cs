// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Result from the signing orchestrator.
    /// </summary>
    public sealed class SigningResult
    {
        /// <summary>
        /// Whether signing completed successfully.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Files that were signed.
        /// </summary>
        public IReadOnlyList<SignedFileInfo> SignedFiles { get; }

        /// <summary>
        /// Errors that occurred during signing.
        /// </summary>
        public IReadOnlyList<SigningError> Errors { get; }

        /// <summary>
        /// Telemetry about the signing process.
        /// </summary>
        public SigningTelemetry Telemetry { get; }

        public SigningResult(
            bool success,
            IReadOnlyList<SignedFileInfo> signedFiles,
            IReadOnlyList<SigningError> errors,
            SigningTelemetry telemetry)
        {
            Success = success;
            SignedFiles = signedFiles ?? throw new ArgumentNullException(nameof(signedFiles));
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
            Telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        }
    }

    /// <summary>
    /// Information about a signed file.
    /// </summary>
    public sealed class SignedFileInfo
    {
        public string FilePath { get; }
        public string Certificate { get; }
        public bool WasAlreadySigned { get; }

        public SignedFileInfo(string filePath, string certificate, bool wasAlreadySigned)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            Certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
            WasAlreadySigned = wasAlreadySigned;
        }
    }

    /// <summary>
    /// Telemetry data from signing.
    /// </summary>
    public sealed class SigningTelemetry
    {
        public int TotalFiles { get; set; }
        public int FilesS​igned { get; set; }
        public int FilesSkipped { get; set; }
        public int SigningRounds { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
