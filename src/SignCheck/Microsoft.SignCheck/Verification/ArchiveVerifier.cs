// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.SignCheck.Logging;

namespace Microsoft.SignCheck.Verification
{
    public class ArchiveVerifier : FileVerifier
    {
        public ArchiveVerifier(Log log, Exclusions exclusions, SignatureVerificationOptions options, string fileExtension) : base(log, exclusions, options, fileExtension)
        {

        }

        /// <summary>
        /// Verify the contents of a zip-based archive and add the results to the container result.
        /// </summary>
        /// <param name="svr">The container result</param>
        protected void VerifyContent(SignatureVerificationResult svr)
        {
            if (VerifyRecursive)
            {
                using (ZipArchive zipArchive = ZipFile.OpenRead(svr.FullPath))
                {
                    string tempPath = svr.TempPath;
                    CreateDirectory(tempPath);
                    Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagExtractingFileContents, tempPath);
                    Dictionary<string, string> archiveMap = new Dictionary<string, string>();

                    foreach (ZipArchiveEntry archiveEntry in zipArchive.Entries)
                    {
                        // Generate an alias for the actual file that has the same extension. We do this to avoid path too long errors so that
                        // containers can be flattened.
                        string directoryName = Path.GetDirectoryName(archiveEntry.FullName);
                        string hashedPath = String.IsNullOrEmpty(directoryName) ? Utils.GetHash(@".\", HashAlgorithmName.MD5.Name) :
                            Utils.GetHash(directoryName, HashAlgorithmName.MD5.Name);
                        string extension = Path.GetExtension(archiveEntry.FullName);

                        // CAB files cannot be aliased since they're referred to from the Media table inside the MSI
                        string aliasFileName = String.Equals(extension.ToLowerInvariant(), ".cab") ? Path.GetFileName(archiveEntry.FullName) :
                            Utils.GetHash(archiveEntry.FullName, HashAlgorithmName.MD5.Name) + Path.GetExtension(archiveEntry.FullName);
                        string aliasFullName = Path.Combine(tempPath, hashedPath, aliasFileName);

                        if (File.Exists(aliasFullName))
                        {
                            Log.WriteMessage(LogVerbosity.Normal, SignCheckResources.FileAlreadyExists, aliasFullName);
                        }
                        else
                        {
                            CreateDirectory(Path.GetDirectoryName(aliasFullName));
                            archiveEntry.ExtractToFile(aliasFullName);
                            archiveMap[archiveEntry.FullName] = aliasFullName;
                        }
                    }

                    // We can only verify once everything is extracted. This is mainly because MSIs can have mutliple external CAB files
                    // and we need to ensure they are extracted before we verify the MSIs.
                    foreach (string fullName in archiveMap.Keys)
                    {
                        SignatureVerificationResult archiveEntryResult = VerifyFile(archiveMap[fullName], svr.Filename, fullName);

                        // Tag the full path into the result detail
                        archiveEntryResult.AddDetail(DetailKeys.File, SignCheckResources.DetailFullName, fullName);
                        svr.NestedResults.Add(archiveEntryResult);
                    }
                    DeleteDirectory(tempPath);
                }
            }
        }
    }
}
