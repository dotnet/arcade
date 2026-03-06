// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    /// <summary>
    /// Parsed result from an ESRP CLI invocation.
    /// </summary>
    internal sealed class ESRPCliResult
    {
        public bool Success { get; }
        public Guid? OperationId { get; }
        public string? ErrorMessage { get; }

        /// <summary>
        /// Individual failure details extracted from the ESRP CLI output.
        /// Each entry describes one failed file/operation, including the OperationId,
        /// status, file path, and raw error JSON.
        /// </summary>
        public IReadOnlyList<string> FailureDetails { get; }

        public ESRPCliResult(bool success, Guid? operationId, string? errorMessage,
            IReadOnlyList<string>? failureDetails = null)
        {
            Success = success;
            OperationId = operationId;
            ErrorMessage = errorMessage;
            FailureDetails = failureDetails ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Parses stdout/stderr from an ESRP CLI invocation.
    /// </summary>
    internal static class ESRPCliResultParser
    {
        private const string OperationIdPrefix = "Calling esrp gateway get status for this operation Id: ";
        private const string FailedFilesHeader = "List of failed files:";

        public static ESRPCliResult Parse(int exitCode, string stdout, string stderr)
        {
            Guid? operationId = null;
            bool foundSuccess = false;
            bool foundFailure = false;
            var failureDetails = new List<string>();

            if (!string.IsNullOrEmpty(stdout))
            {
                bool inFailedFilesSection = false;

                foreach (var line in stdout.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0)
                    {
                        continue;
                    }

                    if (trimmed.StartsWith("Success", StringComparison.OrdinalIgnoreCase))
                    {
                        foundSuccess = true;
                    }

                    if (trimmed.IndexOf("failDoNotRetry", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        foundFailure = true;
                    }

                    // Detect the "List of failed files:" section header
                    if (trimmed.Equals(FailedFilesHeader, StringComparison.OrdinalIgnoreCase))
                    {
                        inFailedFilesSection = true;
                        continue;
                    }

                    // Collect "Failed OperationId: ..." lines as failure details
                    if (inFailedFilesSection &&
                        trimmed.StartsWith("Failed OperationId:", StringComparison.OrdinalIgnoreCase))
                    {
                        failureDetails.Add(trimmed);
                        foundFailure = true;
                    }

                    // Also detect "N files failed." lines
                    if (trimmed.EndsWith("files failed.", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.EndsWith("file failed.", StringComparison.OrdinalIgnoreCase))
                    {
                        foundFailure = true;
                    }

                    var opIdx = trimmed.IndexOf(OperationIdPrefix, StringComparison.OrdinalIgnoreCase);
                    if (opIdx >= 0)
                    {
                        var remainder = trimmed.Substring(opIdx + OperationIdPrefix.Length).Trim();
                        // Operation ID may be followed by additional text
                        var spaceIdx = remainder.IndexOf(' ');
                        var guidStr = spaceIdx >= 0 ? remainder.Substring(0, spaceIdx) : remainder;
                        if (Guid.TryParse(guidStr, out var parsed))
                        {
                            operationId = parsed;
                        }
                    }
                }
            }

            // Non-zero exit code always means failure
            if (exitCode != 0)
            {
                var message = $"ESRP CLI exited with code {exitCode}.";
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    message += $" stderr: {stderr.Trim()}";
                }
                return new ESRPCliResult(false, operationId, message, failureDetails);
            }

            if (foundFailure)
            {
                return new ESRPCliResult(false, operationId, "ESRP CLI reported signing failure(s).", failureDetails);
            }

            if (foundSuccess)
            {
                return new ESRPCliResult(true, operationId, null);
            }

            // No explicit success or failure signal with exit code 0
            return new ESRPCliResult(false, operationId, "ESRP CLI exited with code 0 but did not report success.");
        }
    }
}
