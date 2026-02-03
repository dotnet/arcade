// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.SignCheck.Logging;

namespace Microsoft.SignCheck.Verification
{
    /// <summary>
    /// Verifies detached PGP signatures (.sig files).
    /// A detached signature file must have the same name as the signed file with a .sig extension.
    /// For example, foo.tar.gz would have a signature file foo.tar.gz.sig.
    /// </summary>
    public class SigVerifier : PgpVerifier
    {
        public SigVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options) 
            : base(log, exclusions, options, ".sig")
        {
        }

        protected override (string signatureDocument, string signableContent) GetSignatureDocumentAndSignableContent(string path, string tempDir)
        {
            // The signature file path is the current file (e.g., foo.tar.gz.sig)
            string signatureFilePath = path;
            
            // The signed file should be the signature file without the .sig extension
            string signedFilePath = path.Substring(0, path.Length - FileExtension.Length);

            // Check if the signed file exists
            if (!File.Exists(signedFilePath))
            {
                return (null, null);
            }

            // Copy files to temp directory to avoid issues with spaces in paths
            string tempSigFile = Path.Combine(tempDir, "signature.sig");
            string tempSignedFile = Path.Combine(tempDir, "content");
            
            File.Copy(signatureFilePath, tempSigFile);
            File.Copy(signedFilePath, tempSignedFile);

            return (tempSigFile, tempSignedFile);
        }

        public override SignatureVerificationResult VerifySignature(string path, string parent, string virtualPath)
        {
#if NET
            // The signed file should be the signature file without the .sig extension
            string signedFilePath = path.Substring(0, path.Length - FileExtension.Length);

            // Check if the signed file exists
            if (!File.Exists(signedFilePath))
            {
                Log.WriteMessage(LogVerbosity.Detailed, $"Signed file not found for signature: {signedFilePath}");
                SignatureVerificationResult errorResult = new SignatureVerificationResult(path, parent, virtualPath);
                errorResult.IsSigned = false;
                errorResult.AddDetail(DetailKeys.Error, $"Corresponding file not found: {Path.GetFileName(signedFilePath)}");
                return errorResult;
            }

            SignatureVerificationResult svr = new SignatureVerificationResult(path, parent, virtualPath);

            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            
            try
            {
                Directory.CreateDirectory(tempDir);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.WriteMessage(LogVerbosity.Detailed, $"Failed to create temporary directory: {ex.Message}");
                svr.IsSigned = false;
                svr.AddDetail(DetailKeys.Error, $"Failed to create temporary directory for verification: {ex.Message}");
                return svr;
            }

            try
            {
                (string signatureDocument, string signableContent) = GetSignatureDocumentAndSignableContent(path, tempDir);

                bool isSigned = VerifyPgpSignature(signatureDocument, signableContent, svr, tempDir);
                
                svr.IsSigned = isSigned;
                svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, isSigned);
                if (isSigned)
                {
                    svr.AddDetail(DetailKeys.File, $"Detached signature verified for: {Path.GetFileName(signedFilePath)}");
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Log cleanup failure but don't fail the verification
                    Log.WriteMessage(LogVerbosity.Diagnostic, $"Failed to clean up temporary directory {tempDir}: {ex.Message}");
                }
            }

            return svr;
#else
            // PGP verification is not supported on .NET Framework
            return SignatureVerificationResult.UnsupportedFileTypeResult(path, parent, virtualPath);
#endif
        }
    }
}
