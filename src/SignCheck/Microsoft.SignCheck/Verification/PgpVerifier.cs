// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.SignCheck.Logging;

namespace Microsoft.SignCheck.Verification
{
    /// <summary>
    /// Base class for verifying PGP signatures.
    /// Provides PGP verification logic for files and packages.
    /// </summary>
    public abstract class PgpVerifier : FileVerifier
    {
        protected PgpVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options, string fileExtension) 
            : base(log, exclusions, options, fileExtension)
        {
        }

        /// <summary>
        /// Returns the paths to the signature document and the signable content.
        /// Used to verify the signature using gpg.
        /// </summary>
        /// <param name="path">The path to the file to verify.</param>
        /// <param name="tempDir">A temporary directory for intermediate files.</param>
        /// <returns>A tuple containing the signature document path and the signable content path.</returns>
        protected abstract (string signatureDocument, string signableContent) GetSignatureDocumentAndSignableContent(string path, string tempDir);

#if NET
        /// <summary>
        /// Verifies if the file has a valid PGP signature.
        /// </summary>
        protected virtual bool IsSigned(string path, SignatureVerificationResult svr)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException("PGP signature verification is not supported on Windows.");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            try
            {
                (string signatureDocument, string signableContent) = GetSignatureDocumentAndSignableContent(path, tempDir);

                return VerifyPgpSignature(signatureDocument, signableContent, svr, tempDir);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        /// <summary>
        /// Verifies a PGP signature using GPG.
        /// </summary>
        /// <param name="signatureDocument">Path to the signature file.</param>
        /// <param name="signableContent">Path to the content that was signed.</param>
        /// <param name="svr">The SignatureVerificationResult to populate with details.</param>
        /// <param name="tempDir">Temporary directory for GPG operations.</param>
        /// <returns>True if the signature is valid, false otherwise.</returns>
        public static bool VerifyPgpSignature(string signatureDocument, string signableContent, SignatureVerificationResult svr, string tempDir)
        {
            if (string.IsNullOrEmpty(signatureDocument) || string.IsNullOrEmpty(signableContent))
            {
                return false;
            }

            // https://microsoft.sharepoint.com/teams/prss/esrp/info/SitePages/Linux%20GPG%20Signing.aspx
            Utils.DownloadAndConfigurePublicKeys(tempDir);

            // Escape file paths to prevent shell injection
            string escapedSigFile = EscapeShellArgument(signatureDocument);
            string escapedContentFile = EscapeShellArgument(signableContent);
            
            (int exitCode, string output, string error) = Utils.RunBashCommand($"gpg --verify --status-fd 1 {escapedSigFile} {escapedContentFile}");
            string verificationOutput = output + error;

            if (!verificationOutput.Contains("Good signature"))
            {
                if (exitCode != 0 && !verificationOutput.Contains("no signature found"))
                {
                    // Log an error if something other than a missing
                    // signature caused the verification to fail
                    svr.AddDetail(DetailKeys.Error, error);
                }
                return false;
            }

            Timestamp ts = GetTimestamp(verificationOutput);
            ts.AddToSignatureVerificationResult(svr);
            return ts.IsValid;
        }

        /// <summary>
        /// Get the timestamp of the signature from GPG verification output.
        /// </summary>
        private static Timestamp GetTimestamp(string verificationOutput)
        {
            Regex signatureTimestampsRegex = new Regex(@"VALIDSIG .+ \d+-\d+-\d+ (?<signedOn>\d+) (?<expiresOn>\d+) ");
            Match signatureTimestampsMatch = signatureTimestampsRegex.Match(verificationOutput);

            Regex signatureKeyInfoRegex = new Regex(@"using (?<algorithm>.+) key (?<keyId>.+)");
            Match signatureKeyInfoMatch = signatureKeyInfoRegex.Match(verificationOutput);

            string keyId = signatureKeyInfoMatch.GroupValueOrDefault("keyId");
            
            // Escape keyId to prevent shell injection
            string escapedKeyId = EscapeShellArgument(keyId);
            (_, string keyInfo, _) = Utils.RunBashCommand($"gpg --list-keys --with-colons {escapedKeyId} | grep '^pub:'");
            Regex keyInfoRegex = new Regex(@$"pub.+{Regex.Escape(keyId)}:(?<createdOn>\d+):");
            Match keyInfoMatch = keyInfoRegex.Match(keyInfo);

            return new Timestamp()
            {
                SignedOn = signatureTimestampsMatch.GroupValueOrDefault("signedOn").DateTimeOrDefault(DateTime.MaxValue),
                ExpiryDate = signatureTimestampsMatch.GroupValueOrDefault("expiresOn").DateTimeOrDefault(DateTime.MaxValue),
                SignatureAlgorithm = signatureKeyInfoMatch.GroupValueOrDefault("algorithm"),
                EffectiveDate = keyInfoMatch.GroupValueOrDefault("createdOn").DateTimeOrDefault(DateTime.MaxValue)
            };
        }

        /// <summary>
        /// Escapes a string for use as a shell argument by wrapping it in single quotes
        /// and escaping any single quotes in the string.
        /// </summary>
        private static string EscapeShellArgument(string argument)
        {
            if (string.IsNullOrEmpty(argument))
            {
                return "''";
            }

            // In bash, the safest way to escape is to use single quotes
            // and replace any single quotes with '\''
            return $"'{argument.Replace("'", "'\\''")}'";
        }
#endif
    }
}
