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
    /// Helper class for verifying PGP signatures.
    /// Provides common PGP verification logic that can be used by different verifiers.
    /// </summary>
    internal static class PgpVerificationHelper
    {
#if NET
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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException("PGP signature verification is not supported on Windows.");
            }

            if (string.IsNullOrEmpty(signatureDocument) || string.IsNullOrEmpty(signableContent))
            {
                return false;
            }

            // https://microsoft.sharepoint.com/teams/prss/esrp/info/SitePages/Linux%20GPG%20Signing.aspx
            Utils.DownloadAndConfigurePublicKeys(tempDir);

            (int exitCode, string output, string error) = Utils.RunBashCommand($"gpg --verify --status-fd 1 {signatureDocument} {signableContent}");
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
            (_, string keyInfo, _) = Utils.RunBashCommand($"gpg --list-keys --with-colons {keyId} | grep '^pub:'");
            Regex keyInfoRegex = new Regex(@$"pub.+{keyId}:(?<createdOn>\d+):");
            Match keyInfoMatch = keyInfoRegex.Match(keyInfo);

            return new Timestamp()
            {
                SignedOn = signatureTimestampsMatch.GroupValueOrDefault("signedOn").DateTimeOrDefault(DateTime.MaxValue),
                ExpiryDate = signatureTimestampsMatch.GroupValueOrDefault("expiresOn").DateTimeOrDefault(DateTime.MaxValue),
                SignatureAlgorithm = signatureKeyInfoMatch.GroupValueOrDefault("algorithm"),
                EffectiveDate = keyInfoMatch.GroupValueOrDefault("createdOn").DateTimeOrDefault(DateTime.MaxValue)
            };
        }
#endif
    }
}
