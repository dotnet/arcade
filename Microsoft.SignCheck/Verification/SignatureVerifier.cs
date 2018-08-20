using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Packaging;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Deployment.WindowsInstaller.Package;
using Microsoft.SignCheck.Interop.PortableExecutable;
using Microsoft.SignCheck.Logging;
using Microsoft.Tools.WindowsInstallerXml;

namespace Microsoft.SignCheck.Verification
{
    public class SignatureVerifier
    {
        /// <summary>
        /// A delegate to verify a file's signature.
        /// </summary>
        /// <param name="path">The path of the file to verify.</param>
        /// <param name="parent">The name of the parent container file. Parent may be null if there is no outer container.</param>
        /// <returns>A <see cref="SignatureVerificationResult"/> that contains the result of the verification action.</returns>
        public delegate SignatureVerificationResult VerifyByFileExtension(string path, string parent);

        private List<SignatureVerificationResult> _results;
        private Dictionary<string, VerifyByFileExtension> _verifiers;

        /// <summary>
        /// Enable signature verification for .xml files.
        /// </summary>
        public bool EnableXmlSignatureVerification
        {
            get;
            set;
        }

        /// <summary>
        /// An instance of <see cref="Log"/>.
        /// </summary>
        public Log Log
        {
            get;
            set;
        }

        /// <summary>
        /// A set of files to exclude from signature verification.
        /// </summary>
        public Exclusions Exclusions
        {
            get;
            private set;
        }

        /// <summary>
        /// When set to true, the files inside a container such as an .msi or .vsix will be checked. Set to false to only check top-level files.
        /// </summary>
        public bool Recursive
        {
            get;
            set;
        }

        public List<SignatureVerificationResult> Results
        {
            get
            {
                if (_results == null)
                {
                    _results = new List<SignatureVerificationResult>();
                }

                return _results;
            }
            private set
            {
                _results = value;
            }
        }

        /// <summary>
        /// Enables verification of strong name signatures when set to true.
        /// </summary>
        public bool VerifyStrongName
        {
            get;
            set;
        }

        /// <summary>
        /// When set to true, the timestamps of Authenticode signatures will be skipped.
        /// </summary>
        public bool SkipAuthentiCodeTimestamp
        {
            get;
            set;
        }

        /// <summary>
        /// Controls the verbosity of the output written to the <see cref="Log"/>.
        /// 
        /// </summary>
        public LogVerbosity Verbosity
        {
            get;
            private set;
        }

        /// <summary>
        /// Returns a set of predefined verifiers. The keys of the dictionary corresponds to a specific file extension. New
        /// delegates for verifiers can be added.
        /// </summary>
        public Dictionary<string, VerifyByFileExtension> Verifiers
        {
            get
            {
                if (_verifiers == null)
                {
                    _verifiers = new Dictionary<string, VerifyByFileExtension>();
                }

                return _verifiers;
            }

            private set
            {
                _verifiers = value;
            }
        }

        public SignatureVerifier(LogVerbosity verbosity, Exclusions exclusions, Log log)
        {
            Exclusions = exclusions;
            Log = log;
            Verbosity = verbosity;

            Verifiers.Add(".cab", VerifyCab);
            Verifiers.Add(".dll", VerifyDll);
            Verifiers.Add(".exe", VerifyExe);
            Verifiers.Add(".js", VerifyAuthentiCodeOnly);
            Verifiers.Add(".lzma", VerifyLzma);
            Verifiers.Add(".msi", VerifyMsi);
            Verifiers.Add(".msp", VerifyMsp);
            Verifiers.Add(".nupkg", VerifyNupkg);
            Verifiers.Add(".psd1", VerifyAuthentiCodeOnly);
            Verifiers.Add(".psm1", VerifyAuthentiCodeOnly);
            Verifiers.Add(".ps1", VerifyAuthentiCodeOnly);
            Verifiers.Add(".ps1xml", VerifyAuthentiCodeOnly);
            Verifiers.Add(".vsix", VerifyVsix);
            Verifiers.Add(".xml", VerifyXml);
            Verifiers.Add(".zip", VerifyZip);
        }

        /// <summary>
        /// Verify the signatures of a set of files.
        /// </summary>
        /// <param name="files">A set of files to verify.</param>
        /// <returns>A list of results for each file that was verified.</returns>
        public IEnumerable<SignatureVerificationResult> Verify(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                Results.Add(VerifyFile(file, parent: null));
            }

            return Results;
        }

        /// <summary>
        /// Identify a file by looking at potential headers to determine which built-in verifier to use.
        /// </summary>
        /// <param name="path">The path of the file to verify.</param>
        /// <param name="parent">The parent container or null if this is a top-level file.</param>
        /// <returns></returns>
        public SignatureVerificationResult VerifyByFileHeader(string path, string parent)
        {
            Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagVerifyByFileHeader);

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                try
                {
                    // Test for 4-byte magic numbers
                    if (stream.Length > 4)
                    {
                        uint magic4 = reader.ReadUInt32();
                        if (magic4 == FileHeaders.Zip)
                        {
                            using (var zipArchive = ZipFile.OpenRead(path))
                            {
                                if (zipArchive.Entries.Any(z => String.Equals(Path.GetExtension(z.FullName), "nuspec", StringComparison.OrdinalIgnoreCase)))
                                {
                                    // NUPKGs use .zip format, but should have a .nuspec files inside
                                    Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagFileHeaderIdentifyExtensionType, ".nupkg");
                                    var result = VerifyNupkg(path, parent);
                                    result.AddDetail(DetailKeys.Misc, SignCheckResources.DetailMiscFileType, "NUPKG");
                                    return result;
                                }
                                else
                                {
                                    // Assume it's some sort of .zip file
                                    Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagFileHeaderIdentifyExtensionType, ".zip");
                                    var result = VerifyZip(path, parent);
                                    result.AddDetail(DetailKeys.Misc, SignCheckResources.DetailMiscFileType, "ZIP");
                                    return result;
                                }
                            }

                        }
                        else if (magic4 == FileHeaders.Cab)
                        {
                            var result = VerifyCab(path, parent);
                            result.AddDetail(DetailKeys.Misc, SignCheckResources.DetailMiscFileType, "CAB");
                            return result;
                        }
                    }

                    reader.BaseStream.Seek(0, SeekOrigin.Begin);
                    if (stream.Length > 2)
                    {
                        UInt16 magic2 = reader.ReadUInt16();
                        if (magic2 == FileHeaders.Dos)
                        {
                            PortableExecutableHeader pe = new PortableExecutableHeader(path);

                            if ((pe.FileHeader.Characteristics & ImageFileCharacteristics.IMAGe_FILE_DLL) != 0)
                            {
                                Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagFileHeaderIdentifyExtensionType, ".dll");
                                var result = VerifyDll(path, parent);
                                result.AddDetail(DetailKeys.Misc, SignCheckResources.DetailMiscFileType, "DLL");
                                return result;
                            }
                            else if ((pe.FileHeader.Characteristics & ImageFileCharacteristics.IMAGE_FILE_EXECUTABLE_IMAGE) != 0)
                            {
                                Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagFileHeaderIdentifyExtensionType, ".exe");
                                var result = VerifyExe(path, parent);
                                result.AddDetail(DetailKeys.Misc, SignCheckResources.DetailMiscFileType, "EXE");
                                return result;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.WriteError(e.Message);
                }
            }

            return SignatureVerificationResult.SkippedResult(path, parent);
        }

        /// <summary>
        /// Verify the signature of a single file.
        /// </summary>
        /// <param name="path">The path of the file to verify.</param>
        /// <param name="parent">The name of parent container, e.g. an MSI or VSIX. Can be null when there is no parent container.</param>
        /// <returns>The verification result.</returns>
        public SignatureVerificationResult VerifyFile(string path, string parent)
        {
            Log.WriteMessage(LogVerbosity.Detailed, String.Format(SignCheckResources.ProcessingFile, Path.GetFileName(path), String.IsNullOrEmpty(parent) ? SignCheckResources.NA : parent));
            var extension = Path.GetExtension(path);
            VerifyByFileExtension verifierDelegate;
            SignatureVerificationResult result;

            if (Verifiers.TryGetValue(extension, out verifierDelegate))
            {
                result = verifierDelegate(path, parent);
            }
            else
            {
                result = VerifyByFileHeader(path, parent);
            }

            Log.WriteMessage(LogVerbosity.Diagnostic, String.Format(SignCheckResources.DiagFirstExclusion, path));
            if (Exclusions.Count > 0)
            {
                result.IsExcluded = Exclusions.IsParentExcluded(parent) | Exclusions.IsFileExcluded(path);
            }

            // Include the full path for top-level files
            if (String.IsNullOrEmpty(parent))
            {
                result.AddDetail(DetailKeys.File, SignCheckResources.DetailFullName, result.FullPath);
            }

            return result;
        }

        public SignatureVerificationResult VerifyAuthentiCode(string path, string parent)
        {
            SignatureVerificationResult svr = new SignatureVerificationResult(path, parent);
            svr.IsAuthentiCodeSigned = AuthentiCode.IsSigned(path);
            svr.IsSigned = svr.IsAuthentiCodeSigned;

            if (!SkipAuthentiCodeTimestamp)
            {
                try
                {
                    svr.Timestamps = AuthentiCode.GetTimestamps(path).ToList();

                    foreach (var timestamp in svr.Timestamps)
                    {
                        svr.AddDetail(DetailKeys.AuthentiCode, SignCheckResources.DetailTimestamp, timestamp.SignedOn, timestamp.SignatureAlgorithm);
                        svr.IsAuthentiCodeSigned &= timestamp.IsValid;
                    }
                }
                catch
                {
                    svr.AddDetail(DetailKeys.AuthentiCode, SignCheckResources.DetailTimestampError);
                    svr.IsSigned = false;
                }
            }
            else
            {
                svr.AddDetail(DetailKeys.AuthentiCode, SignCheckResources.DetailTimestampSkipped);
            }

            svr.AddDetail(DetailKeys.AuthentiCode, SignCheckResources.DetailSignedAuthentiCode, svr.IsAuthentiCodeSigned);

            return svr;
        }

        public SignatureVerificationResult VerifyAuthentiCodeOnly(string path, string parent)
        {
            var svr = VerifyAuthentiCode(path, parent);
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);
            return svr;
        }

        public void VerifyStrongNameSignature(SignatureVerificationResult svr, PortableExecutableHeader portableExecutableHeader)
        {
            if (portableExecutableHeader.IsManagedCode)
            {
                svr.IsNativeImage = !portableExecutableHeader.IsILImage;
                // NGEN/CrossGen don't preserve StrongName signatures.
                if (!svr.IsNativeImage)
                {
                    bool wasVerified = false;
                    int hresult = StrongName.ClrStrongName.StrongNameSignatureVerificationEx(svr.FullPath, fForceVerification: true, pfWasVerified: out wasVerified);
                    svr.IsStrongNameSigned = hresult == StrongName.S_OK;
                    svr.AddDetail(DetailKeys.StrongName, SignCheckResources.DetailSignedStrongName, svr.IsStrongNameSigned);

                    if (hresult != StrongName.S_OK)
                    {
                        svr.AddDetail(DetailKeys.StrongName, SignCheckResources.DetailHResult, hresult);
                    }
                    else
                    {
                        string publicToken;
                        hresult = StrongName.GetStrongNameTokenFromAssembly(svr.FullPath, out publicToken);
                        if (hresult == StrongName.S_OK)
                        {
                            svr.AddDetail(DetailKeys.StrongName, SignCheckResources.DetailPublicKeyToken, publicToken);
                        }
                    }
                }
                else
                {
                    svr.AddDetail(DetailKeys.StrongName, SignCheckResources.DetailNativeImage);
                }
            }
        }

        public SignatureVerificationResult VerifyCab(string path, string parent)
        {
            var svr = VerifyAuthentiCode(path, parent);
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);
            return svr;
        }

        public SignatureVerificationResult VerifyDll(string path, string parent)
        {
            SignatureVerificationResult svr = VerifyAuthentiCode(path, parent);

            if (VerifyStrongName)
            {
                var dllHeader = new PortableExecutableHeader(svr.FullPath);
                VerifyStrongNameSignature(svr, dllHeader);
            }

            svr.IsSigned = svr.IsAuthentiCodeSigned & ((svr.IsStrongNameSigned) || (!VerifyStrongName) || svr.IsNativeImage);
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);

            return svr;
        }

        public SignatureVerificationResult VerifyExe(string path, string parent)
        {
            SignatureVerificationResult svr = VerifyAuthentiCode(path, parent);
            // Always retrieve the PE header of an EXE since we have multiple switches that might
            // require access to the information
            var exeHeader = new PortableExecutableHeader(svr.FullPath);
            if (VerifyStrongName)
            {
                VerifyStrongNameSignature(svr, exeHeader);
            }

            svr.IsSigned = svr.IsAuthentiCodeSigned & ((svr.IsStrongNameSigned) || (!VerifyStrongName));
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);

            if (Recursive)
            {
                if (exeHeader.ImageSectionHeaders.Select(s => s.SectionName).Contains(".wixburn"))
                {
                    Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagSectionHeader, ".wixburn");
                    Log.WriteMessage(LogVerbosity.Detailed, SignCheckResources.WixBundle, svr.FullPath);
                    Unbinder unbinder = null;

                    try
                    {
                        Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagExtractingFileContents, svr.TempPath);
                        unbinder = new Unbinder();
                        unbinder.Message += UnbinderEventHandler;
                        var o = unbinder.Unbind(svr.FullPath, OutputType.Bundle, svr.TempPath);

                        if (Directory.Exists(svr.TempPath))
                        {
                            foreach (var file in Directory.EnumerateFiles(svr.TempPath, "*.*", SearchOption.AllDirectories))
                            {
                                svr.NestedResults.Add(VerifyFile(Path.GetFullPath(file), svr.Filename));
                            }
                        }

                        Directory.Delete(svr.TempPath, recursive: true);
                    }
                    finally
                    {
                        unbinder.DeleteTempFiles();
                    }
                }
            }

            // TODO: Check for SFXCAB, IronMan, etc.

            return svr;
        }

        public SignatureVerificationResult VerifyLzma(string path, string parent)
        {
            var svr = SignatureVerificationResult.SkippedResult(path, parent);
            var fullPath = svr.FullPath;
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, SignCheckResources.NA);

            if (Recursive)
            {
                var tempPath = svr.TempPath;
                CreateDirectory(tempPath);
                Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagExtractingFileContents, tempPath);

                // Drop the LZMA extensions when decompressing so we don't process the uncompressed file as an LZMA file
                var destinationFile = Path.Combine(tempPath, Path.GetFileNameWithoutExtension(path));

                // LZMA files are just compressed streams. Decompress and then try to verify the file.
                LZMAUtils.Decompress(fullPath, destinationFile);

                svr.NestedResults.Add(VerifyFile(destinationFile, parent));
            }

            return svr;
        }

        public SignatureVerificationResult VerifyNupkg(string path, string parent)
        {
            SignatureVerificationResult svr = new SignatureVerificationResult(path, parent);
            var fullPath = svr.FullPath;

            svr.IsSigned = NuGetPackage.IsSigned(fullPath);
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);

            if (Recursive)
            {
                using (var zipArchive = ZipFile.OpenRead(fullPath))
                {
                    var tempPath = svr.TempPath;
                    CreateDirectory(tempPath);
                    Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagExtractingFileContents, tempPath);

                    foreach (var archiveEntry in zipArchive.Entries)
                    {
                        // Generate an alias for the actual file that has the same extension. We do this to avoid path too long errors so that
                        // containers can be flattened
                        var aliasFileName = Utils.GetHash(archiveEntry.FullName, HashAlgorithmName.MD5.Name) + Path.GetExtension(archiveEntry.FullName);
                        var aliasFullName = Path.Combine(tempPath, aliasFileName);

                        archiveEntry.ExtractToFile(aliasFullName);
                        var archiveEntryResult = VerifyFile(aliasFullName, svr.Filename);

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

        public SignatureVerificationResult VerifyMsi(string path, string parent)
        {
            SignatureVerificationResult svr = VerifyAuthentiCode(path, parent);
            svr.IsSigned = svr.IsAuthentiCodeSigned;
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);

            if (Recursive)
            {
                CreateDirectory(svr.TempPath);

                // TODO: Fix for MSIs with external CABs that are not present.
                using (var installPackage = new InstallPackage(svr.FullPath, DatabaseOpenMode.Transact, sourceDir: null, workingDir: svr.TempPath))
                {
                    var files = installPackage.Files;
                    var originalFiles = new Dictionary<string, string>();

                    // Flatten the files to avoid path too long errors. We use the File column and extension to create a unique file
                    // and record the original, relative MSI path in the result.
                    foreach (var key in installPackage.Files.Keys)
                    {
                        originalFiles[key] = installPackage.Files[key].TargetPath;
                        var name = key + Path.GetExtension(installPackage.Files[key].TargetName);
                        var targetPath = Path.Combine(svr.TempPath, name);
                        installPackage.Files[key].TargetName = name;
                        installPackage.Files[key].SourceName = name;
                        installPackage.Files[key].SourcePath = targetPath;
                        installPackage.Files[key].TargetPath = targetPath;
                    }

                    try
                    {
                        Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagExtractingFileContents, svr.TempPath);
                        installPackage.ExtractFiles(installPackage.Files.Keys);

                        foreach (var key in installPackage.Files.Keys)
                        {
                            var packageFileResult = VerifyFile(installPackage.Files[key].TargetPath, svr.Filename);
                            packageFileResult.AddDetail(DetailKeys.File, SignCheckResources.DetailFullName, originalFiles[key]);
                            CheckAndUpdateExclusion(packageFileResult, packageFileResult.Filename, originalFiles[key], svr.Filename);
                            svr.NestedResults.Add(packageFileResult);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.WriteError(e.Message);
                    }
                }

                DeleteDirectory(svr.TempPath);
            }

            return svr;
        }

        public SignatureVerificationResult VerifyMsp(string path, string parent)
        {
            var svr = VerifyAuthentiCode(path, parent);
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);

            if (Recursive)
            {
                Interop.StructuredStorage.OpenAndExtractStorages(path, svr.TempPath);

                foreach (var file in Directory.EnumerateFiles(svr.TempPath))
                {
                    svr.NestedResults.Add(VerifyFile(file, svr.Filename));
                }

                DeleteDirectory(svr.TempPath);
            }

            return svr;
        }

        public SignatureVerificationResult VerifyVsix(string path, string parent)
        {
            var svr = new SignatureVerificationResult(path, parent);
            var fullPath = svr.FullPath;
            PackageDigitalSignature packageSignature;

            svr.IsSigned = VsixPackage.IsSigned(fullPath, out packageSignature);
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);

            if (Recursive)
            {
                using (var zipArchive = ZipFile.OpenRead(fullPath))
                {
                    var tempPath = svr.TempPath;
                    CreateDirectory(tempPath);

                    foreach (var archiveEntry in zipArchive.Entries)
                    {
                        // Generate an alias for the actual file that has the same extension. We do this to avoid path too long errors so that
                        // containers can be flattened
                        var aliasFileName = Utils.GetHash(archiveEntry.FullName, HashAlgorithmName.MD5.Name) + Path.GetExtension(archiveEntry.FullName);
                        var aliasFullName = Path.Combine(tempPath, aliasFileName);

                        archiveEntry.ExtractToFile(aliasFullName);
                        var archiveEntryResult = VerifyFile(aliasFullName, svr.Filename);

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

        public SignatureVerificationResult VerifyXml(string path, string parent)
        {
            if (EnableXmlSignatureVerification)
            {
                X509Certificate2 xmlCertificate;
                var svr = new SignatureVerificationResult(path, parent);
                svr.IsSigned = Xml.IsSigned(svr.FullPath, out xmlCertificate);
                svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);

                return svr;
            }

            return SignatureVerificationResult.SkippedResult(path, parent);
        }

        public SignatureVerificationResult VerifyZip(string path, string parent)
        {
            var svr = SignatureVerificationResult.SkippedResult(path, parent);
            var fullPath = svr.FullPath;
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, SignCheckResources.NA);

            if (Recursive)
            {
                using (var zipArchive = ZipFile.OpenRead(fullPath))
                {
                    var tempPath = svr.TempPath;
                    CreateDirectory(tempPath);
                    Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagExtractingFileContents, tempPath);

                    foreach (var archiveEntry in zipArchive.Entries)
                    {
                        // Generate an alias for the actual file that has the same extension. We do this to avoid path too long errors so that
                        // containers can be flattened
                        var aliasFileName = Utils.GetHash(archiveEntry.FullName, HashAlgorithmName.MD5.Name) + Path.GetExtension(archiveEntry.FullName);
                        var aliasFullName = Path.Combine(tempPath, aliasFileName);

                        if (File.Exists(aliasFullName))
                        {
                            Log.WriteMessage(LogVerbosity.Normal, SignCheckResources.FileAlreadyExists, aliasFullName);
                        }
                        else
                        {
                            archiveEntry.ExtractToFile(aliasFullName);
                            var archiveEntryResult = VerifyFile(aliasFullName, svr.Filename);

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

        private void CheckAndUpdateExclusion(SignatureVerificationResult result, string alias, string fullName, string parent)
        {
            if (Exclusions.Count > 0)
            {
                result.IsExcluded |= Exclusions.IsFileExcluded(fullName);
            }

            result.ExclusionEntry = String.Join(";", String.Join("|", alias, fullName), parent, String.Empty);
            Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagGenerateExclusion, result.Filename, result.ExclusionEntry);
        }

        private void CreateDirectory(string path)
        {
            Log.WriteMessage(LogVerbosity.Diagnostic, String.Format(SignCheckResources.DiagCreatingFolder, path));
            Directory.CreateDirectory(path);
        }

        private void DeleteDirectory(string path)
        {
            Log.WriteMessage(LogVerbosity.Diagnostic, String.Format(SignCheckResources.DiagDeletingFolder, path));
            Directory.Delete(path, recursive: true);
        }

        private void UnbinderEventHandler(object sender, MessageEventArgs e)
        {
            Log.WriteMessage(LogVerbosity.Detailed, String.Format("{0}|{1}|{2}|{3}", e.Id, e.Level, e.ResourceName, e.SourceLineNumbers));
        }
    }
}
