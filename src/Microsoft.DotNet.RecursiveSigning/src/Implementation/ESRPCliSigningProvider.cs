// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.DotNet.RecursiveSigning.Abstractions;
using Microsoft.DotNet.RecursiveSigning.Models;

namespace Microsoft.DotNet.RecursiveSigning.Implementation
{
    /// <summary>
    /// Signing provider that invokes the ESRP CLI tool.
    /// Builds a single submission JSON containing one <c>SignBatch</c> per distinct certificate
    /// and makes a single CLI invocation per <see cref="SignFilesAsync"/> call.
    /// Supports a dry-run mode that logs the submission without invoking the CLI.
    /// </summary>
    public sealed class ESRPCliSigningProvider : ISigningProvider
    {
        private readonly ESRPCliSigningConfiguration _configuration;
        private readonly IProcessRunner _processRunner;
        private readonly ILogger<ESRPCliSigningProvider> _logger;

        private static readonly JsonSerializerOptions s_jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public ESRPCliSigningProvider(
            ESRPCliSigningConfiguration configuration,
            IProcessRunner processRunner,
            ILogger<ESRPCliSigningProvider> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> SignFilesAsync(
            IReadOnlyList<(FileNode node, string outputPath)> files,
            CancellationToken cancellationToken = default)
        {
            if (files == null || files.Count == 0)
            {
                return true;
            }

            // Build the submission JSON
            var submissionJson = BuildSubmissionJson(files);

            if (_configuration.DryRun)
            {
                var templateArgs = BuildArgumentsTemplate("<workDir>");
                _logger.LogInformation("=== ESRP CLI Dry Run ===");
                _logger.LogInformation("Submission JSON:\n{Json}", submissionJson);
                _logger.LogInformation("CLI arguments (template):\n  dotnet {Args}", templateArgs);
                _logger.LogInformation("Files to sign: {Count}", files.Count);
                foreach (var (node, outputPath) in files)
                {
                    _logger.LogInformation("  {File} => {Cert}",
                        node.Location.FilePathOnDisk,
                        node.CertificateIdentifier?.Name ?? "(none)");
                }
                _logger.LogInformation("=== End Dry Run ===");
                return true;
            }

            // Create a unique working directory
            var workDir = Path.Combine(_configuration.TempDirectory, Guid.NewGuid().ToString());
            Directory.CreateDirectory(workDir);

            try
            {
                // Write submission JSON
                var submissionPath = Path.Combine(workDir, "submission.json");
                File.WriteAllText(submissionPath, submissionJson);

                // Write pattern file (comma-separated relative paths for all files)
                var patternPath = Path.Combine(workDir, "pattern.txt");
                var pattern = BuildPatternFile(files);
                File.WriteAllText(patternPath, pattern);

                // Build real arguments with actual work directory
                var realArguments = BuildArguments(workDir);

                _logger.LogDebug("ESRP CLI arguments: dotnet {Args}", RedactAuthArguments(realArguments));
                _logger.LogInformation("Signing {Count} file(s) via ESRP CLI", files.Count);

                var result = await _processRunner.RunAsync("dotnet", realArguments, cancellationToken);

                _logger.LogDebug("ESRP CLI stdout:\n{Stdout}", result.StandardOutput);
                if (!string.IsNullOrWhiteSpace(result.StandardError))
                {
                    _logger.LogDebug("ESRP CLI stderr:\n{Stderr}", result.StandardError);
                }

                var parsed = ESRPCliResultParser.Parse(result.ExitCode, result.StandardOutput, result.StandardError);

                if (parsed.OperationId.HasValue)
                {
                    _logger.LogInformation("ESRP operation ID: {OperationId}", parsed.OperationId.Value);
                }

                if (!parsed.Success)
                {
                    _logger.LogError("ESRP CLI signing failed: {Error}", parsed.ErrorMessage);
                    return false;
                }

                _logger.LogInformation("ESRP CLI signing succeeded for {Count} file(s)", files.Count);
                return true;
            }
            finally
            {
                try
                {
                    Directory.Delete(workDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to delete working directory {WorkDir}: {Error}", workDir, ex.Message);
                }
            }
        }

        /// <summary>
        /// Builds the ESRP submission JSON with one SignBatch per distinct certificate.
        /// This is an internal method exposed for testing.
        /// </summary>
        internal string BuildSubmissionJson(IReadOnlyList<(FileNode node, string outputPath)> files)
        {
            // Group files by certificate friendly name
            var groups = new Dictionary<string, (ESRPCertificateIdentifier cert, List<(FileNode node, string outputPath)> files)>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in files)
            {
                var certId = entry.node.CertificateIdentifier as ESRPCertificateIdentifier
                    ?? throw new InvalidOperationException(
                        $"File '{entry.node.Location.FilePathOnDisk}' has a CertificateIdentifier that is not an ESRPCertificateIdentifier.");

                if (!groups.TryGetValue(certId.FriendlyName, out var group))
                {
                    group = (certId, new List<(FileNode, string)>());
                    groups[certId.FriendlyName] = group;
                }

                group.files.Add(entry);
            }

            // Build SignBatches - each batch uses the common root of its files
            var signBatches = new List<object>();
            foreach (var (_, (cert, groupFiles)) in groups)
            {
                var batchRootDir = ComputeCommonRoot(groupFiles.Select(f => NormalizePath(f.node.Location.FilePathOnDisk!)));

                var signRequestFiles = new List<object>();
                foreach (var (node, outputPath) in groupFiles)
                {
                    var relativePath = GetRelativePath(node.Location.FilePathOnDisk!, batchRootDir);
                    signRequestFiles.Add(new
                    {
                        CustomerCorrelationId = Guid.NewGuid().ToString(),
                        SourceLocation = relativePath,
                        DestinationLocation = relativePath,
                    });
                }

                signBatches.Add(new SignBatchEntry
                {
                    SourceLocationType = "UNC",
                    SourceRootDirectory = batchRootDir,
                    DestinationLocationType = "UNC",
                    DestinationRootDirectory = batchRootDir,
                    SignRequestFiles = signRequestFiles.ToArray(),
                    SigningInfo = new SigningInfoEntry
                    {
                        Operations = ExtractOperations(cert.CertificateDefinition),
                    },
                });
            }

            var submission = new SubmissionEntry
            {
                Version = "1.0.0",
                SignBatches = signBatches.ToArray(),
            };

            return JsonSerializer.Serialize(submission, s_jsonOptions);
        }

        internal string BuildPatternFile(IReadOnlyList<(FileNode node, string outputPath)> files)
        {
            var allPaths = files.Select(f => NormalizePath(f.node.Location.FilePathOnDisk!));
            var commonRoot = ComputeCommonRoot(allPaths);
            var sb = new StringBuilder();
            for (int i = 0; i < files.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append(GetRelativePath(files[i].node.Location.FilePathOnDisk!, commonRoot));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Builds a display-only template of CLI arguments for dry-run output.
        /// Does not write any files or perform encryption.
        /// </summary>
        private string BuildArgumentsTemplate(string workDir)
        {
            var keyVaultJson = JsonSerializer.Serialize(new { akv = _configuration.KeyVaultName, cert = _configuration.CertificateName });

            var sb = new StringBuilder();
            sb.Append($"--roll-forward Major {_configuration.ESRPCliPath} vsts.sign");
            sb.Append(" -x regularSigning");
            sb.Append(" -y \"inlineSignParams\"");
            sb.Append($" -c {_configuration.BatchSize}");
            sb.Append($" -j \"{Path.Combine(workDir, "submission.json")}\"");
            sb.Append(" -u false");
            sb.Append($" -f \"{_configuration.RootDirectory}\"");
            sb.Append($" -p \"{Path.Combine(workDir, "pattern.txt")}\"");
            sb.Append($" -m {_configuration.BatchSize}");
            sb.Append($" -t {_configuration.TimeoutInMinutes}");
            sb.Append(" -v \"Tls12\"");
            sb.Append($" -s \"{_configuration.GatewayUrl}\"");
            sb.Append($" -o \"{_configuration.Organization}\"");
            sb.Append($" -i \"{_configuration.OrganizationInfoUrl}\"");
            sb.Append(" -r true");
            sb.Append($" -a {_configuration.ClientId}");
            sb.Append($" -d {_configuration.TenantId}");
            sb.Append($" -z {JsonSerializer.Serialize(keyVaultJson)}");

            if (_configuration.AuthMode == ESRPAuthMode.FederatedToken)
            {
                sb.Append(" -useMSIAuthentication true");
                sb.Append(" -federatedTokenData [REDACTED]");
            }
            else
            {
                sb.Append(" -useMSIAuthentication false");
                sb.Append(" -encryptedCertificateData [REDACTED]");
            }

            return sb.ToString();
        }

        internal string BuildArguments(string workDir)
        {
            var keyVaultJson = JsonSerializer.Serialize(new { akv = _configuration.KeyVaultName, cert = _configuration.CertificateName });

            var sb = new StringBuilder();
            sb.Append($"--roll-forward Major {_configuration.ESRPCliPath} vsts.sign");
            sb.Append(" -x regularSigning");
            sb.Append(" -y \"inlineSignParams\"");
            sb.Append($" -c {_configuration.BatchSize}");
            sb.Append($" -j \"{Path.Combine(workDir, "submission.json")}\"");
            sb.Append(" -u false");
            sb.Append($" -f \"{_configuration.RootDirectory}\"");
            sb.Append($" -p \"{Path.Combine(workDir, "pattern.txt")}\"");
            sb.Append($" -m {_configuration.BatchSize}");
            sb.Append($" -t {_configuration.TimeoutInMinutes}");
            sb.Append(" -v \"Tls12\"");
            sb.Append($" -s \"{_configuration.GatewayUrl}\"");
            sb.Append($" -o \"{_configuration.Organization}\"");
            sb.Append($" -i \"{_configuration.OrganizationInfoUrl}\"");
            sb.Append(" -r true");
            sb.Append($" -a {_configuration.ClientId}");
            sb.Append($" -d {_configuration.TenantId}");
            sb.Append($" -z {JsonSerializer.Serialize(keyVaultJson)}");

            // Authentication
            if (_configuration.AuthMode == ESRPAuthMode.FederatedToken)
            {
                sb.Append(" -useMSIAuthentication true");
                var federatedTokenData = BuildFederatedTokenData();
                sb.Append($" -federatedTokenData {JsonSerializer.Serialize(federatedTokenData)}");
            }
            else
            {
                sb.Append(" -useMSIAuthentication false");
                var encryptedCertData = JsonSerializer.Serialize(new
                {
                    authCert = _configuration.EncryptedAuthCertPath,
                    encryptionKey = _configuration.EncryptionKeyPath
                });
                sb.Append($" -encryptedCertificateData {JsonSerializer.Serialize(encryptedCertData)}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds the federated token data JSON for PME auth.
        /// The systemAccessToken field is the name of an environment variable
        /// containing the ADO system access token. The ESRP CLI reads the token
        /// from this env var at runtime.
        /// </summary>
        private string BuildFederatedTokenData()
        {
            var tokenEnvVar = _configuration.SystemAccessTokenEnvVar;

            if (string.IsNullOrEmpty(tokenEnvVar))
            {
                throw new InvalidOperationException(
                    "SystemAccessTokenEnvVar must be set for FederatedToken auth mode. " +
                    "Set it to the name of the environment variable containing the ADO system access token (e.g. 'SYSTEM_ACCESSTOKEN').");
            }

            var json = new
            {
                jobId = Environment.GetEnvironmentVariable("SYSTEM_JOBID") ?? "",
                planId = Environment.GetEnvironmentVariable("SYSTEM_PLANID") ?? "",
                projectId = Environment.GetEnvironmentVariable("SYSTEM_TEAMPROJECTID") ?? "",
                hub = Environment.GetEnvironmentVariable("SYSTEM_HOSTTYPE") ?? "",
                uri = Environment.GetEnvironmentVariable("SYSTEM_COLLECTIONURI")
                    ?? Environment.GetEnvironmentVariable("SYSTEM_TEAMFOUNDATIONCOLLECTIONURI") ?? "",
                serviceConnectionId = _configuration.ServiceConnectionId,
                systemAccessToken = tokenEnvVar,
            };

            return JsonSerializer.Serialize(json);
        }

        internal static string ComputeCommonRoot(IEnumerable<string> normalizedPaths)
        {
            string? common = null;
            foreach (var path in normalizedPaths)
            {
                var dir = path.Substring(0, path.LastIndexOf('/'));
                if (common == null)
                {
                    common = dir;
                }
                else
                {
                    while (!dir.StartsWith(common + "/", StringComparison.OrdinalIgnoreCase) &&
                           !dir.Equals(common, StringComparison.OrdinalIgnoreCase))
                    {
                        var lastSlash = common.LastIndexOf('/');
                        common = lastSlash >= 0 ? common.Substring(0, lastSlash) : common;
                        if (common.Length <= 1)
                        {
                            break;
                        }
                    }
                }
            }

            return common ?? string.Empty;
        }

        /// <summary>
        /// Extracts the "operations" array from a CertificateDefinition JsonElement.
        /// The CertificateDefinition is the full cert entry: {"friendlyName":"...", "operations":[...]}.
        /// We need just the operations array for the ESRP submission.
        /// </summary>
        private static JsonElement ExtractOperations(JsonElement certificateDefinition)
        {
            if (certificateDefinition.ValueKind == JsonValueKind.Array)
            {
                // Already an array of operations
                return certificateDefinition;
            }

            if (certificateDefinition.ValueKind == JsonValueKind.Object &&
                certificateDefinition.TryGetProperty("operations", out var operations))
            {
                return operations.Clone();
            }

            throw new InvalidOperationException(
                "CertificateDefinition must be either an operations array or an object with an 'operations' property.");
        }

        private static string GetRelativePath(string filePath, string rootDir)
        {
            var normalizedFile = NormalizePath(filePath);
            if (!normalizedFile.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"File path '{filePath}' is not under root directory '{rootDir}'.");
            }

            var relative = normalizedFile.Substring(rootDir.Length);
            if (relative.StartsWith("/") || relative.StartsWith("\\"))
            {
                relative = relative.Substring(1);
            }

            return relative;
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
        }

        private static string RedactAuthArguments(string arguments)
        {
            // Redact auth-sensitive portions
            var redacted = arguments;
            foreach (var flag in new[] { "-federatedTokenData", "-encryptedCertificateData" })
            {
                var idx = redacted.IndexOf(flag, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var endIdx = redacted.IndexOf(" -", idx + flag.Length);
                    if (endIdx < 0)
                    {
                        endIdx = redacted.Length;
                    }
                    redacted = redacted.Substring(0, idx + flag.Length) + " [REDACTED]" + redacted.Substring(endIdx);
                }
            }
            return redacted;
        }

        // Internal DTOs for JSON serialization of the submission
        internal sealed class SubmissionEntry
        {
            [JsonPropertyName("Version")]
            public string Version { get; set; } = "1.0.0";

            [JsonPropertyName("SignBatches")]
            public object[] SignBatches { get; set; } = Array.Empty<object>();
        }

        internal sealed class SignBatchEntry
        {
            [JsonPropertyName("SourceLocationType")]
            public string SourceLocationType { get; set; } = "UNC";

            [JsonPropertyName("SourceRootDirectory")]
            public string SourceRootDirectory { get; set; } = string.Empty;

            [JsonPropertyName("DestinationLocationType")]
            public string DestinationLocationType { get; set; } = "UNC";

            [JsonPropertyName("DestinationRootDirectory")]
            public string DestinationRootDirectory { get; set; } = string.Empty;

            [JsonPropertyName("SignRequestFiles")]
            public object[] SignRequestFiles { get; set; } = Array.Empty<object>();

            [JsonPropertyName("SigningInfo")]
            public SigningInfoEntry SigningInfo { get; set; } = new();
        }

        internal sealed class SigningInfoEntry
        {
            [JsonPropertyName("Operations")]
            public JsonElement Operations { get; set; }
        }
    }
}
