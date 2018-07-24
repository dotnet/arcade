using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Deployment.WindowsInstaller.Package;
using Microsoft.SignCheck.Interop;
using Microsoft.SignCheck.Logging;
using Microsoft.Tools.WindowsInstallerXml;
using System;
using System.Xml;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.IO.Packaging;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;

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
        public Dictionary<string, Exclusion> Exclusions
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
        /// When set to true, strongname verification of managed code files will be skipped.
        /// </summary>
        public bool SkipStrongName
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

        public SignatureVerifier(LogVerbosity verbosity, Dictionary<string, Exclusion> exclusions, Log log)
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
        /// Verify the signature of a single file.
        /// </summary>
        /// <param name="path">The path of the file to verify.</param>
        /// <param name="parent">The name of parent container, e.g. an MSI or VSIX. Can be null when there is no parent container.</param>
        /// <returns>The verification result.</returns>
        public SignatureVerificationResult VerifyFile(string path, string parent)
        {
            var extension = Path.GetExtension(path);

            if (!String.IsNullOrEmpty(parent))
            {
                Log.WriteMessage(LogVerbosity.Detailed, String.Format(SignCheckResources.ProcessingFile, Path.GetFileName(path), parent));
            }
            else
            {
                Log.WriteMessage(LogVerbosity.Detailed, String.Format(SignCheckResources.ProcessingFile, Path.GetFileName(path), SignCheckResources.NA));
            }

            VerifyByFileExtension verifierDelegate;
            if (Verifiers.TryGetValue(extension, out verifierDelegate))
            {
                return verifierDelegate(path, parent);
            }

            return SignatureVerificationResult.SkippedResult(path);
        }

        public SignatureVerificationResult VerifyAuthentiCode(string path, string parent)
        {
            var signatureVerificationResult = new SignatureVerificationResult(path, Exclusions, parent);
            var isAuthentiCodeSigned = AuthentiCode.IsAuthentiCodeSigned(signatureVerificationResult.FullPath);

            signatureVerificationResult.AddDetail(SignCheckResources.DetailAuthentiCode, Convert.ToString(isAuthentiCodeSigned));

            // If we are checking timestamps this result may change
            signatureVerificationResult.IsSigned = isAuthentiCodeSigned;

            if (isAuthentiCodeSigned && !SkipAuthentiCodeTimestamp)
            {
                IEnumerable<Timestamp> timestamps = null;
                Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagCheckingTimestamps);

                try
                {
                    timestamps = AuthentiCode.GetTimestamps(path);

                    foreach (var timestamp in timestamps)
                    {
                        signatureVerificationResult.AddDetail(SignCheckResources.DetailTimestamp, timestamp.SignedOn, timestamp.SignatureAlgorithm);
                    }
                }
                catch
                {
                    signatureVerificationResult.AddDetail(SignCheckResources.DetailNoTimetamp);
                }

                bool hasValidTimestamps = timestamps.All(o => o.IsValid);

                signatureVerificationResult.IsSigned &= hasValidTimestamps;
            }
            else
            {
                signatureVerificationResult.AddDetail(SignCheckResources.DetailTimestampSkipped);
            }

            return signatureVerificationResult;
        }

        public SignatureVerificationResult VerifyCab(string path, string parent)
        {
            var signatureVerificationResult = VerifyAuthentiCode(path, parent);
            signatureVerificationResult.AddDetail(SignCheckResources.DetailSigned, Convert.ToString(signatureVerificationResult.IsSigned));

            return signatureVerificationResult;
        }

        public SignatureVerificationResult VerifyExe(string path, string parent)
        {
            var signatureVerificationResult = VerifyAuthentiCode(path, parent);

            if (StrongName.IsManagedCode(signatureVerificationResult.FullPath))
            {
                if (!SkipStrongName)
                {
                    Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagCheckingStrongName);
                    var isStrongNameSigned = StrongName.IsStrongNameSigned(signatureVerificationResult.FullPath);
                    signatureVerificationResult.IsSigned &= isStrongNameSigned;

                    var snToken = StrongName.GetStrongNameTokenFromAssembly(signatureVerificationResult.FullPath);

                    signatureVerificationResult.AddDetail(SignCheckResources.DetailSigned, Convert.ToString(signatureVerificationResult.IsSigned));
                    signatureVerificationResult.AddDetail(SignCheckResources.DetailStrongName, Convert.ToString(isStrongNameSigned),
                        String.IsNullOrEmpty(snToken) ? "null" : snToken,
                        StrongName.GetStrongNameTokenFromAssembly(signatureVerificationResult.FullPath));
                }
                else
                {
                    Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagSkippingStrongName);
                    signatureVerificationResult.AddDetail(SignCheckResources.DetailSigned, Convert.ToString(signatureVerificationResult.IsSigned));
                    signatureVerificationResult.AddDetail(SignCheckResources.DetailStrongName, "Skipped", "n/a");
                }
            }

            if (Recursive)
            {
                var exeImage = new PortableExecutableImage(signatureVerificationResult.FullPath);

                if (exeImage.SectionHeaders.Select(s => s.SectionName).Contains(".wixburn"))
                {
                    Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagSectionHeader, ".wixburn");
                    Log.WriteMessage(LogVerbosity.Detailed, SignCheckResources.WixBundle, signatureVerificationResult.FullPath);
                    Unbinder unbinder = null;

                    try
                    {
                        Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagExtractingFileContents, signatureVerificationResult.TempPath);
                        unbinder = new Unbinder();
                        unbinder.Message += UnbinderEventHandler;
                        var o = unbinder.Unbind(signatureVerificationResult.FullPath, OutputType.Bundle, signatureVerificationResult.TempPath);

                        if (Directory.Exists(signatureVerificationResult.TempPath))
                        {
                            foreach (var file in Directory.EnumerateFiles(signatureVerificationResult.TempPath, "*.*", SearchOption.AllDirectories))
                            {
                                signatureVerificationResult.NestedResults.Add(VerifyFile(Path.GetFullPath(file), signatureVerificationResult.Filename));
                            }
                        }

                        Directory.Delete(signatureVerificationResult.TempPath, recursive: true);
                    }
                    finally
                    {
                        unbinder.DeleteTempFiles();
                    }
                }
            }

            // TODO: Check for SFXCAB, IronMan, etc.

            return signatureVerificationResult;
        }

        public SignatureVerificationResult VerifyDll(string path, string parent)
        {
            var signatureVerificationResult = VerifyAuthentiCode(path, parent);

            if (StrongName.IsManagedCode(signatureVerificationResult.FullPath))
            {
                if (!SkipStrongName)
                {
                    Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagCheckingStrongName);
                    var isStrongNameSigned = StrongName.IsStrongNameSigned(signatureVerificationResult.FullPath);
                    signatureVerificationResult.IsSigned &= isStrongNameSigned;
                    var snToken = StrongName.GetStrongNameTokenFromAssembly(signatureVerificationResult.FullPath);

                    signatureVerificationResult.AddDetail(SignCheckResources.DetailSigned, Convert.ToString(signatureVerificationResult.IsSigned));
                    signatureVerificationResult.AddDetail(SignCheckResources.DetailStrongName, Convert.ToString(isStrongNameSigned),
                        String.IsNullOrEmpty(snToken) ? "null" : snToken,
                        StrongName.GetStrongNameTokenFromAssembly(signatureVerificationResult.FullPath));
                }
                else
                {
                    Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagSkippingStrongName);
                    signatureVerificationResult.AddDetail(SignCheckResources.DetailSigned, Convert.ToString(signatureVerificationResult.IsSigned));
                    signatureVerificationResult.AddDetail(SignCheckResources.DetailStrongName, "Skipped", "n/a");
                }
            }

            return signatureVerificationResult;
        }

        public SignatureVerificationResult VerifyNupkg(string path, string parent)
        {
            var result = new SignatureVerificationResult(path, Exclusions, parent);
            var fullPath = result.FullPath;

            result.IsSigned = NuGetPackage.IsSigned(fullPath);
            result.AddDetail(SignCheckResources.DetailSigned, Convert.ToString(result.IsSigned));

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
                        archiveEntryResult.AddDetail(SignCheckResources.DetailFullName, archiveEntry.FullName);

                        result.NestedResults.Add(archiveEntryResult);
                    }

                    DeleteDirectory(tempPath);
                }
            }

            return result;
        }

        public SignatureVerificationResult VerifyMsi(string path, string parent)
        {
            var result = VerifyAuthentiCode(path, parent);

            if (Recursive)
            {
                CreateDirectory(result.TempPath);
                Log.WriteMessage(LogVerbosity.Diagnostic, SignCheckResources.DiagExtractingFileContents, result.TempPath);

                using (var installPackage = new InstallPackage(result.FullPath, DatabaseOpenMode.Transact, sourceDir: null, workingDir: result.TempPath))
                {
                    var files = installPackage.Files;
                    var originalFiles = new Dictionary<string, string>();

                    // Flatten the files to avoid path too long errors. We use the File column and extension to create a unique file
                    // and record the original, relative MSI path in the result.
                    foreach (var key in installPackage.Files.Keys)
                    {
                        originalFiles[key] = installPackage.Files[key].TargetPath;
                        var name = key + Path.GetExtension(installPackage.Files[key].TargetName);
                        var targetPath = Path.Combine(result.TempPath, name);
                        installPackage.Files[key].TargetName = name;
                        installPackage.Files[key].SourceName = name;
                        installPackage.Files[key].SourcePath = targetPath;
                        installPackage.Files[key].TargetPath = targetPath;
                    }

                    installPackage.ExtractFiles(installPackage.Files.Keys);

                    foreach (var key in installPackage.Files.Keys)
                    {
                        var packageFileResult = VerifyFile(installPackage.Files[key].TargetPath, result.Filename);
                        packageFileResult.AddDetail(SignCheckResources.DetailFullName, originalFiles[key]);
                        result.NestedResults.Add(packageFileResult);
                    }
                }

                DeleteDirectory(result.TempPath);
            }

            return result;
        }

        public SignatureVerificationResult VerifyMsp(string path, string parent)
        {
            var result = VerifyAuthentiCode(path, parent);

            return result;
        }

        public SignatureVerificationResult VerifyZip(string path, string parent)
        {
            var result = new SignatureVerificationResult(path, Exclusions, parent);
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
                        archiveEntryResult.AddDetail(SignCheckResources.DetailFullName, archiveEntry.FullName);
                        result.NestedResults.Add(archiveEntryResult);
                    }

                    DeleteDirectory(tempPath);
                }
            }

            return result;
        }

        public SignatureVerificationResult VerifyVsix(string path, string parent)
        {
            var result = new SignatureVerificationResult(path, Exclusions, parent);
            var fullPath = result.FullPath;
            PackageDigitalSignature packageSignature;

            result.IsSigned = VsixPackage.IsSigned(fullPath, out packageSignature);
            result.AddDetail(SignCheckResources.DetailSigned, Convert.ToString(result.IsSigned));

            if (Recursive)
            {
                using (var zipArchive = ZipFile.OpenRead(fullPath))
                {
                    var tempPath = result.TempPath;
                    CreateDirectory(tempPath);

                    foreach (var archiveEntry in zipArchive.Entries)
                    {
                        // Generate an alias for the actual file that has the same extension. We do this to avoid path too long errors so that
                        // containers can be flattened
                        var aliasFileName = Utils.GetHash(archiveEntry.FullName, HashAlgorithmName.MD5.Name) + Path.GetExtension(archiveEntry.FullName);
                        var aliasFullName = Path.Combine(tempPath, aliasFileName);

                        archiveEntry.ExtractToFile(aliasFullName);
                        var archiveEntryResult = VerifyFile(aliasFullName, result.Filename);

                        // Tag the full path into the result detail
                        archiveEntryResult.AddDetail(SignCheckResources.DetailFullName, archiveEntry.FullName);
                        result.NestedResults.Add(archiveEntryResult);
                    }

                    DeleteDirectory(tempPath);
                }
            }

            return result;
        }

        public SignatureVerificationResult VerifyXml(string path, string parent)
        {
            var result = new SignatureVerificationResult(path, Exclusions, parent);
            X509Certificate2 xmlCertificate;
            result.IsSigned = Xml.IsSigned(result.FullPath, out xmlCertificate);

            return result;
        }

        private void UnbinderEventHandler(object sender, MessageEventArgs e)
        {
            Log.WriteMessage(LogVerbosity.Detailed, String.Format("{0}|{1}|{2}|{3}", e.Id, e.Level, e.ResourceName, e.SourceLineNumbers));
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
    }
}
