using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.SignCheck.Logging;

namespace Microsoft.SignCheck.Verification
{
    public class ZipVerifier : FileVerifier
    {
        public ZipVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options) : base(log, exclusions, options, fileExtension: ".zip")
        {

        }

        public override SignatureVerificationResult VerifySignature(string path, string parent)
        {
            var svr = SignatureVerificationResult.UnsupportedFileTypeResult(path, parent);
            string fullPath = svr.FullPath;
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, SignCheckResources.NA);

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
                        string aliasFileName = Utils.GetHash(archiveEntry.FullName, HashAlgorithmName.MD5.Name) + Path.GetExtension(archiveEntry.FullName);
                        string aliasFullName = Path.Combine(tempPath, aliasFileName);

                        if (File.Exists(aliasFullName))
                        {
                            Log.WriteMessage(LogVerbosity.Normal, SignCheckResources.FileAlreadyExists, aliasFullName);
                        }
                        else
                        {
                            archiveEntry.ExtractToFile(aliasFullName);
                            SignatureVerificationResult archiveEntryResult = VerifyFile(aliasFullName, svr.Filename);

                            // Tag the full path into the result detail
                            archiveEntryResult.AddDetail(DetailKeys.File, SignCheckResources.DetailFullName, archiveEntry.FullName);
                            CheckAndUpdateExclusion(archiveEntryResult, aliasFileName, archiveEntry.FullName, svr.Filename);
                            svr.NestedResults.Add(archiveEntryResult);
                        }
                    }

                    DeleteDirectory(tempPath);
                }
            }

            return svr;
        }
    }
}
