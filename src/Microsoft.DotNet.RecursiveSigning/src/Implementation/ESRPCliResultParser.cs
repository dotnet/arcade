// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;

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

        public ESRPCliResult(bool success, Guid? operationId, string? errorMessage)
        {
            Success = success;
            OperationId = operationId;
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// Parses stdout/stderr from an ESRP CLI invocation.
    /// </summary>
    internal static class ESRPCliResultParser
    {
        private const string OperationIdPrefix = "Calling esrp gateway get status for this operation Id: ";

        public static ESRPCliResult Parse(int exitCode, string stdout, string stderr)
        {
            Guid? operationId = null;
            bool foundSuccess = false;
            bool foundFailure = false;

            if (!string.IsNullOrEmpty(stdout))
            {
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
                return new ESRPCliResult(false, operationId, message);
            }

            if (foundFailure)
            {
                return new ESRPCliResult(false, operationId, "ESRP CLI reported failDoNotRetry.");
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
