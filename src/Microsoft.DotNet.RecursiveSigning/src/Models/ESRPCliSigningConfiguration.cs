// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.RecursiveSigning.Models
{
    /// <summary>
    /// Configuration for the ESRP CLI signing provider.
    /// </summary>
    public sealed class ESRPCliSigningConfiguration
    {
        /// <summary>
        /// Path to esrpcli.dll.
        /// </summary>
        public string ESRPCliPath { get; set; } = string.Empty;

        /// <summary>
        /// ESRP gateway URL.
        /// </summary>
        public string GatewayUrl { get; set; } = "https://api.esrp.microsoft.com/api/v2";

        /// <summary>
        /// ESRP client ID for signing requests.
        /// PME default: c4428c30-2253-4654-ab5e-cdb9ff6d850c
        /// CORP default: dec434ad-6bd4-4a20-b400-4bf6db7fc3fb
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// AAD tenant ID.
        /// PME default: 975f013f-7f24-47e8-a7d3-abc4752bf346
        /// CORP default: 72f988bf-86f1-41af-91ab-2d7cd011db47
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Organization name.
        /// </summary>
        public string Organization { get; set; } = "Microsoft";

        /// <summary>
        /// Organization info URL.
        /// </summary>
        public string OrganizationInfoUrl { get; set; } = "https://www.microsoft.com";

        /// <summary>
        /// Key vault name for the ESRP request-signing (PKITA) certificate.
        /// </summary>
        public string KeyVaultName { get; set; } = string.Empty;

        /// <summary>
        /// Certificate name in the key vault.
        /// </summary>
        public string CertificateName { get; set; } = string.Empty;

        /// <summary>
        /// Authentication mode.
        /// </summary>
        public ESRPAuthMode AuthMode { get; set; }

        /// <summary>
        /// For CORP mode: path to the AES-encrypted auth certificate file.
        /// </summary>
        public string EncryptedAuthCertPath { get; set; } = string.Empty;

        /// <summary>
        /// For CORP mode: path to the AES encryption key file (JSON with "key" and "iv").
        /// </summary>
        public string EncryptionKeyPath { get; set; } = string.Empty;

        /// <summary>
        /// For PME mode: Azure DevOps service connection ID (typically from MBSIGN_CONNECTEDSERVICE env var).
        /// </summary>
        public string ServiceConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// For PME mode: name of the environment variable containing the ADO system access token.
        /// The ESRP CLI reads the token from this env var at runtime.
        /// Typically "SYSTEM_ACCESSTOKEN" or a custom variable like "ESRP_TOKEN".
        /// </summary>
        public string SystemAccessTokenEnvVar { get; set; } = "SYSTEM_ACCESSTOKEN";

        /// <summary>
        /// Submission timeout in minutes.
        /// </summary>
        public int TimeoutInMinutes { get; set; } = 30;

        /// <summary>
        /// Max batch size hint passed to the ESRP CLI.
        /// </summary>
        public int BatchSize { get; set; } = 400;

        /// <summary>
        /// Temp directory for working files (pattern files, submission JSON, encrypted auth artifacts).
        /// </summary>
        public string TempDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Root directory that file paths are made relative to (the ESRP CLI -f flag).
        /// </summary>
        public string RootDirectory { get; set; } = string.Empty;

        /// <summary>
        /// When true, the provider logs the submission JSON and CLI arguments without invoking the ESRP CLI.
        /// </summary>
        public bool DryRun { get; set; }
    }

    /// <summary>
    /// ESRP authentication mode.
    /// </summary>
    public enum ESRPAuthMode
    {
        /// <summary>
        /// PME: uses ADO service connection with AES-encrypted system access token.
        /// </summary>
        FederatedToken,

        /// <summary>
        /// CORP: uses pre-provisioned encrypted certificate files.
        /// </summary>
        Certificate,
    }
}
