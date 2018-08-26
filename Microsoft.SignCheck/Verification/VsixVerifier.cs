using System.IO;
using System.IO.Compression;
using System.IO.Packaging;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.SignCheck.Logging;

namespace Microsoft.SignCheck.Verification
{
    public class VsixVerifier : FileVerifier
    {
        public VsixVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options) : base(log, exclusions, options, fileExtension: ".vsix")
        {

        }

        public override SignatureVerificationResult VerifySignature(string path, string parent)
        {
            var svr = new SignatureVerificationResult(path, parent);
            string fullPath = svr.FullPath;
            svr.IsSigned = IsSigned(fullPath);
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);

            if (VerifyRecursive)
            {
                using (ZipArchive zipArchive = ZipFile.OpenRead(fullPath))
                {
                    string tempPath = svr.TempPath;
                    CreateDirectory(tempPath);

                    foreach (ZipArchiveEntry archiveEntry in zipArchive.Entries)
                    {
                        // Generate an alias for the actual file, but keep the original extension. This should limit the chances of running
                        // into 'path too long' errors when extracting the files.
                        string aliasFileName = Utils.GetHash(archiveEntry.FullName, HashAlgorithmName.MD5.Name) + Path.GetExtension(archiveEntry.FullName);
                        string aliasFullName = Path.Combine(tempPath, aliasFileName);

                        archiveEntry.ExtractToFile(aliasFullName);
                        SignatureVerificationResult archiveEntryResult = VerifyFile(aliasFullName, svr.Filename);

                        // Tag the full path into the result detail
                        archiveEntryResult.AddDetail(DetailKeys.File, SignCheckResources.DetailFullName, archiveEntry.FullName);
                        CheckAndUpdateExclusion(archiveEntryResult, aliasFileName, archiveEntry.FullName, svr.Filename);
                        svr.NestedResults.Add(archiveEntryResult);
                    }

                    DeleteDirectory(tempPath);
                }
            }

            return svr;
        }

        private bool IsSigned(string path)
        {
            PackageDigitalSignature packageSignature = null;

            using (var vsixStream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var vsixPackage = Package.Open(vsixStream);
                var signatureManager = new PackageDigitalSignatureManager(vsixPackage);

                if (!signatureManager.IsSigned)
                {
                    return false;
                }

                if (signatureManager.Signatures.Count() != 1)
                {
                    return false;
                }

                if (signatureManager.Signatures[0].SignedParts.Count != vsixPackage.GetParts().Count() - 1)
                {
                    return false;
                }

                packageSignature = signatureManager.Signatures[0];
            }

            return true;
        }
    }
}
