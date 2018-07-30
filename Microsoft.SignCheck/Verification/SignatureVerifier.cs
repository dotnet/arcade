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
using Microsoft.SignCheck.Interop;
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
            Verifiers.Add(".js", VerifyAuthentiCode);
            Verifiers.Add(".msi", VerifyMsi);
            Verifiers.Add(".msp", VerifyMsp);
            Verifiers.Add(".nupkg", VerifyNupkg);
            Verifiers.Add(".psd1", VerifyAuthentiCode);
            Verifiers.Add(".psm1", VerifyAuthentiCode);
            Verifiers.Add(".ps1", VerifyAuthentiCode);
            Verifiers.Add(".ps1xml", VerifyAuthentiCode);
            Verifiers.Add(".vsix", VerifyVsix);
            Verifiers.Add(".zip", VerifyZip);

            if (EnableXmlSignatureVerification)
            {
                Verifiers.Add(".xml", VerifyXml);
            }
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
                result.IsExcluded = Exclusions.IsParentExcluded(parent) | Exclusions.IsFileExcluded(path);
            }
            else
            {
                result = SignatureVerificationResult.SkippedResult(path, parent);
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

        public void VerifyStrongNameSignature(SignatureVerificationResult svr)
        {
            if (StrongName.IsManagedCode(svr.FullPath))
            {
                if (VerifyStrongName)
                {
                    string publicToken = StrongName.GetStrongNameTokenFromAssembly(svr.FullPath);
                    svr.AddDetail(DetailKeys.StrongName, SignCheckResources.DetailPublicKeyToken, publicToken);

                    bool wasVerified = false;
                    int hresult = StrongName.ClrStrongName.StrongNameSignatureVerificationEx(svr.FullPath, fForceVerification: true, pfWasVerified: out wasVerified);
                    svr.IsStrongNameSigned = hresult == StrongName.S_OK;

                    // Crossgen'd asemblies return bad image format results.
                    if (hresult != StrongName.S_OK)
                    {
                        svr.AddDetail(DetailKeys.StrongName, SignCheckResources.DetailHResult, hresult);
                    }
                    else
                    {
                        svr.AddDetail(DetailKeys.StrongName, SignCheckResources.DetailSignedStrongName, svr.IsStrongNameSigned);
                    }
                }
                else
                {
                    svr.AddDetail(DetailKeys.StrongName, SignCheckResources.DetailSkipped);
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
            VerifyStrongNameSignature(svr);

            svr.IsSigned = svr.IsAuthentiCodeSigned & ((svr.IsStrongNameSigned) || (!VerifyStrongName));
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);

            return svr;
        }

        public SignatureVerificationResult VerifyExe(string path, string parent)
        {
            SignatureVerificationResult svr = VerifyAuthentiCode(path, parent);
            VerifyStrongNameSignature(svr);

            svr.IsSigned = svr.IsAuthentiCodeSigned & ((svr.IsStrongNameSigned) || (!VerifyStrongName));
            svr.AddDetail(DetailKeys.File, SignCheckResources.DetailSigned, svr.IsSigned);

            if (Recursive)
            {
                var exeImage = new PortableExecutableImage(svr.FullPath);

                if (exeImage.SectionHeaders.Select(s => s.SectionName).Contains(".wixburn"))
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
                Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagExtractingFileContents, svr.TempPath);

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

                    installPackage.ExtractFiles(installPackage.Files.Keys);

                    foreach (var key in installPackage.Files.Keys)
                    {
                        var packageFileResult = VerifyFile(installPackage.Files[key].TargetPath, svr.Filename);
                        //packageFileResult.AddDetail(SignCheckResources.DetailFullName, originalFiles[key]);
                        CheckAndUpdateExclusion(packageFileResult, packageFileResult.Filename, originalFiles[key], svr.Filename);
                        svr.NestedResults.Add(packageFileResult);
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
            var result = new SignatureVerificationResult(path, parent);
            var fullPath = result.FullPath;

            if (Recursive)
            {
                using (var zipArchive = ZipFile.OpenRead(fullPath))
                {
                    var tempPath = result.TempPath;
                    CreateDirectory(tempPath);
                    Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagExtractingFileContents, tempPath);

                    foreach (var archiveEntry in zipArchive.Entries)
                    {
                        // Generate an alias for the actual file that has the same extension. We do this to avoid path too long errors so that
                        // containers can be flattened
                        var aliasFileName = Utils.GetHash(archiveEntry.FullName, HashAlgorithmName.MD5.Name) + Path.GetExtension(archiveEntry.FullName);
                        var aliasFullName = Path.Combine(tempPath, aliasFileName);

                        archiveEntry.ExtractToFile(aliasFullName);
                        var archiveEntryResult = VerifyFile(aliasFullName, result.Filename);

                        // Tag the full path into the result detail
                        archiveEntryResult.AddDetail(DetailKeys.File, SignCheckResources.DetailFullName, archiveEntry.FullName);
                        CheckAndUpdateExclusion(archiveEntryResult, aliasFileName, archiveEntry.FullName, result.Filename);
                        result.NestedResults.Add(archiveEntryResult);
                    }

                    DeleteDirectory(tempPath);
                }
            }

            return result;
        }


        private void CheckAndUpdateExclusion(SignatureVerificationResult result, string alias, string fullName, string parent)
        {
            result.IsExcluded |= Exclusions.IsFileExcluded(fullName);
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
