// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.SignCheck.Logging;

namespace Microsoft.SignCheck.Verification
{
    /// <summary>
    /// Base class for Linux package verifiers (RPM, DEB) that use PGP signatures.
    /// </summary>
    public abstract class LinuxPackageVerifier : ArchiveVerifier
    {
        protected LinuxPackageVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options, string fileExtension) : base(log, exclusions, options, fileExtension) { }

        public override SignatureVerificationResult VerifySignature(string path, string parent, string virtualPath)
            => VerifySupportedFileType(path, parent, virtualPath);

        /// <summary>
        /// Returns the paths to the signature document and the signable content.
        /// Used to verify the signature of the package using gpg.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="tempDir"></param>
        /// <returns></returns>
        protected abstract (string signatureDocument, string signableContent) GetSignatureDocumentAndSignableContent(string path, string tempDir);

        protected override bool IsSigned(string path, SignatureVerificationResult svr)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            try
            {
                (string signatureDocument, string signableContent) = GetSignatureDocumentAndSignableContent(path, tempDir);

                return PgpVerificationHelper.VerifyPgpSignature(signatureDocument, signableContent, svr, tempDir);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
