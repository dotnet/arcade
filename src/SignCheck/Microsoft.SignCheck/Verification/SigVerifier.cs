// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.SignCheck.Logging;

namespace Microsoft.SignCheck.Verification
{
    /// <summary>
    /// Verifies detached PGP signatures (.sig files).
    /// A detached signature file must have the same name as the signed file with a .sig extension.
    /// For example, foo.tar.gz would have a signature file foo.tar.gz.sig.
    /// </summary>
    public class SigVerifier : FileVerifier
    {
        public SigVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options) 
            : base(log, exclusions, options, ".sig")
        {
        }

        public override SignatureVerificationResult VerifySignature(string path, string parent, string virtualPath)
        {
            // The signature file path is the current file (e.g., foo.tar.gz.sig)
            string signatureFilePath = path;
            
            // The signed file should be the signature file without the .sig extension
            string signedFilePath = path.Substring(0, path.Length - FileExtension.Length);

            // Check if the signed file exists
            if (!File.Exists(signedFilePath))
            {
                Log.WriteMessage(LogVerbosity.Detailed, $"Signed file not found: {signedFilePath}");
                return SignatureVerificationResult.UnsignedFileResult(path, parent, virtualPath);
            }

            SignatureVerificationResult svr = new SignatureVerificationResult(path, parent, virtualPath);

            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Copy files to temp directory to avoid issues with spaces in paths
                string tempSigFile = Path.Combine(tempDir, "signature.sig");
                string tempSignedFile = Path.Combine(tempDir, "content");
                
                File.Copy(signatureFilePath, tempSigFile);
                File.Copy(signedFilePath, tempSignedFile);

                bool isSigned = PgpVerificationHelper.VerifyPgpSignature(tempSigFile, tempSignedFile, svr, tempDir);
                
                if (isSigned)
                {
                    svr.IsSigned = true;
                    svr.AddDetail(DetailKeys.File, $"Detached signature verified for: {Path.GetFileName(signedFilePath)}");
                }
                else
                {
                    svr.IsSigned = false;
                }
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }

            return svr;
        }
    }
}
