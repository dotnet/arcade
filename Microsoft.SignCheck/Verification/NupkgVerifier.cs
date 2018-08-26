using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SignCheck.Logging;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Signing;
using Cryptography = System.Security.Cryptography;

namespace Microsoft.SignCheck.Verification
{
    public class NupkgVerifier : FileVerifier
    {
        private static readonly string _fingerPrint = "3F9001EA83C560D712C24CF213C3D312CB3BFF51EE89435D3430BD06B5D0EECE";

        public NupkgVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options) : base(log, exclusions, options, fileExtension: ".nupkg")
        {

        }

        public override SignatureVerificationResult VerifySignature(string path, string parent)
        {
            SignatureVerificationResult svr = new SignatureVerificationResult(path, parent);
            string fullPath = svr.FullPath;

            svr.IsSigned = IsSigned(fullPath);
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);

            if (VerifyRecursive)
            {
                using (ZipArchive zipArchive = ZipFile.OpenRead(fullPath))
                {
                    string tempPath = svr.TempPath;
                    CreateDirectory(tempPath);
                    Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagExtractingFileContents, tempPath);

                    foreach (ZipArchiveEntry archiveEntry in zipArchive.Entries)
                    {
                        // Generate an alias for the actual file that has the same extension. We do this to avoid path too long errors so that
                        // containers can be flattened
                        string aliasFileName = Utils.GetHash(archiveEntry.FullName, Cryptography.HashAlgorithmName.MD5.Name) + Path.GetExtension(archiveEntry.FullName);
                        string aliasFullName = Path.Combine(tempPath, aliasFileName);

                        archiveEntry.ExtractToFile(aliasFullName);
                        SignatureVerificationResult archiveEntryResult = VerifyFile(aliasFullName, svr.Filename);

                        // VerifyFile will set IsExcluded if the filename or parent matches, but it's possible the exclusion was
                        // based on the archive entry's full path.
                        CheckAndUpdateExclusion(archiveEntryResult, aliasFileName, archiveEntry.FullName, svr.Filename);

                        // Tag the full path into the result detail
                        archiveEntryResult.AddDetail(DetailKeys.File, SignCheckResources.DetailFullName, archiveEntry.FullName);

                        svr.NestedResults.Add(archiveEntryResult);
                    }

                    DeleteDirectory(tempPath);
                }
            }

            return svr;
        }

        private bool IsSigned(string path)
        {
            IEnumerable<ISignatureVerificationProvider> providers = SignatureVerificationProviderFactory.GetSignatureVerificationProviders();
            var packageSignatureVerifier = new PackageSignatureVerifier(providers);

            var allowListEntries = new List<CertificateHashAllowListEntry>();
            allowListEntries.Add(new CertificateHashAllowListEntry(VerificationTarget.Author | VerificationTarget.Repository,
                SignaturePlacement.PrimarySignature, _fingerPrint, HashAlgorithmName.SHA256));

            var verifierSettings = SignedPackageVerifierSettings.GetVerifyCommandDefaultPolicy(clientAllowListEntries: allowListEntries);
            IEnumerable<ISignatureVerificationProvider> verificationProviders = SignatureVerificationProviderFactory.GetSignatureVerificationProviders();
            var verifier = new PackageSignatureVerifier(verificationProviders);

            using (var pr = new PackageArchiveReader(path))
            {
                Task<VerifySignaturesResult> verifySignatureResult = packageSignatureVerifier.VerifySignaturesAsync(pr, verifierSettings, CancellationToken.None);

                return verifySignatureResult.Result.Valid;
            }
        }
    }
}
