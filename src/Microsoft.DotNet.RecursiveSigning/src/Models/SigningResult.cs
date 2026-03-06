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
        public int FilesSigned { get; set; }
        public int FilesSkipped { get; set; }
        public int DuplicateFiles { get; set; }
        public int SigningRounds { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan DiscoveryDuration { get; set; }
        public TimeSpan SigningDuration { get; set; }
        public TimeSpan FinalizationDuration { get; set; }

        /// <summary>
        /// Per-round timing: each entry is (signingTime, repackTime, filesInRound).
        /// </summary>
        public IReadOnlyList<SigningRoundTelemetry> Rounds { get; set; } = Array.Empty<SigningRoundTelemetry>();
    }

    /// <summary>
    /// Telemetry for a single signing round.
    /// </summary>
    public sealed class SigningRoundTelemetry
    {
        public int RoundNumber { get; set; }
        public int FilesSigned { get; set; }
        public int ContainersRepacked { get; set; }
        public TimeSpan SigningDuration { get; set; }
        public TimeSpan RepackDuration { get; set; }
    }
}
