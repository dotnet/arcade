// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.SignTool
{
#if NET472
    [LoadInSeparateAppDomain]
    public class SignToolTask : AppDomainIsolatedTask
    {
        static SignToolTask() => AssemblyResolution.Initialize();
#else
    public class SignToolTask : Task
    {
#endif
        /// <summary>
        /// Perform validation but do not actually send signing request to the server.
        /// </summary>
        public bool DryRun { get; set; }

        /// <summary>
        /// True to test sign, otherwise real sign.
        /// </summary>
        public bool TestSign { get; set; }

        /// <summary>
        /// True to perform strong name check on signed files.
        /// If enabled it will require SNBinaryPath to be informed.
        /// </summary>
        public bool DoStrongNameCheck { get; set; }

        /// <summary>
        /// Allow the sign tool task to be called with an empty list of files to be signed.
        /// </summary>
        public bool AllowEmptySignList { get; set; }

        /// <summary>
        /// By default in non-DryRun cases we verify the vsix and nuget packages contain a signature file
        /// This option disables that check in cases you want to sign the container at a later step. 
        /// </summary>
        public bool SkipZipContainerSignatureMarkerCheck { get; set; }

        /// <summary>
        /// For some cases you may need to run the sign tool more than once and if you do you want to
        /// share the same cache directory which contains already signed binaries. In those cases
        /// set this property to true to reuse that file cache.
        /// </summary>
        public bool ReadExistingContainerSigningCache { get; set; }

        /// <summary>
        /// Use the content hash in the path of the extracted file paths. 
        /// The default is to use a unique content id based on the number of items extracted.
        /// </summary>
        public bool UseHashInExtractionPath { get; set; }

        /// <summary>
        /// Working directory used for storing files created during signing.
        /// </summary>
        [Required]
        public string TempDir { get; set; }

        /// <summary>
        /// Path to MicroBuild.Core package directory.
        /// </summary>
        [Required]
        public string MicroBuildCorePath { get; set; }

        /// <summary>
        /// Explicit list of containers / files to be signed.
        /// This needs to be the full path to the file to be signed.
        /// </summary>
        [Required]
        public string[] ItemsToSign { get; set; }

        /// <summary>
        /// List of file names that should be ignored when checking
        /// for correctness of strong name signature.
        /// </summary>
        public string[] ItemsToSkipStrongNameCheck { get; set; }

        /// <summary>
        /// Mapping relating PublicKeyToken, CertificateName and Strong Name. 
        /// Metadata required: PublicKeyToken, CertificateName and Include (which will be the Strong Name)
        /// During signing Certificate and Strong Name will be looked up here based on PublicKeyToken.
        /// </summary>
        public ITaskItem[] StrongNameSignInfo { get; set; }

        /// <summary>
        /// Let the user override the default certificate used for Signing.
        /// Metadata required: CertificateName, TargetFramework, Include (which is the name of the file+extension to be signed)
        /// </summary>
        public ITaskItem[] FileSignInfo { get; set; }

        /// <summary>
        /// This is a mapping between extension (in the format ".ext") to certificate names.
        /// Any file that have its extension listed on this map will be signed with the
        /// specified certificate. This parameter specifies only the *default* certificate name,
        /// overriding this default sign info is possible using the other parameters.
        /// Metadata required: Certificate and Include which is a semicolon separated list of extensions.
        /// </summary>
        public ITaskItem[] FileExtensionSignInfo { get; set; }

        /// <summary>
        /// This is a list describing attributes for each used certificate.
        /// Currently attributes are: 
        ///     DualSigningAllowed:boolean - Tells whether this certificate can be used to sign already signed files.
        /// </summary>
        public ITaskItem[] CertificatesSignInfo { get; set; }

        /// <summary>
        /// Path to msbuild.exe. Required if <see cref="DryRun"/> is <c>false</c>.
        /// </summary>
        public string MSBuildPath { get; set; }

        /// <summary>
        /// Path to sn.exe. Required if strong name signing files locally is needed.
        /// </summary>
        public string SNBinaryPath { get; set; }

        /// <summary>
        /// Directory to write log to.
        /// </summary>
        [Required]
        public string LogDir { get; set; }

        public override bool Execute()
        {
#if NET472
            AssemblyResolution.Log = Log;
#endif
            try
            {
                ExecuteImpl();
                return !Log.HasLoggedErrors;
            }
            finally
            {
#if NET472
                AssemblyResolution.Log = null;
#endif
                Log.LogMessage(MessageImportance.High, "SignToolTask execution finished.");
            }
        }

        public void ExecuteImpl()
        {
            if (!DryRun && typeof(object).Assembly.GetName().Name != "mscorlib" && !File.Exists(MSBuildPath))
            {
                Log.LogError($"MSBuild was not found at this path: '{MSBuildPath}'.");
                return;
            }

            if (!AllowEmptySignList && ItemsToSign.Count() == 0)
            {
                Log.LogWarning(subcategory: null,
                    warningCode: SigningToolErrorCode.SIGN003.ToString(),
                    helpKeyword: null,
                    file: null,
                    lineNumber: 0,
                    columnNumber: 0,
                    endLineNumber: 0,
                    endColumnNumber: 0,
                    message: $"An empty list of files to sign was passed as parameter.");
            }

            if (!DryRun)
            {
                var isValidSNPath = !string.IsNullOrEmpty(SNBinaryPath) && File.Exists(SNBinaryPath) && SNBinaryPath.EndsWith("sn.exe");

                if (DoStrongNameCheck && !isValidSNPath)
                {
                    Log.LogError($"An incorrect full path to 'sn.exe' was specified: {SNBinaryPath}");
                    return;
                }

                var strongNameLocally = StrongNameSignInfo != null 
                    && StrongNameSignInfo
                        .Where(ti => !string.IsNullOrEmpty(ti.ItemSpec) && ti.ItemSpec.EndsWith(".snk", StringComparison.OrdinalIgnoreCase))
                        .Any();

                if (!isValidSNPath && strongNameLocally)
                {
                    Log.LogError($"An incorrect full path to 'sn.exe' was specified: {SNBinaryPath}");
                    return;
                }
            }

            var enclosingDir = GetEnclosingDirectoryOfItemsToSign();

            PrintConfigInformation();

            if (Log.HasLoggedErrors) return;

            var strongNameInfo = ParseStrongNameSignInfo();
            var fileSignInfo = ParseFileSignInfo();
            var extensionSignInfo = ParseFileExtensionSignInfo();
            var dualCertificates = ParseCertificateInfo();

            if (Log.HasLoggedErrors) return;

            var signToolArgs = new SignToolArgs(TempDir, MicroBuildCorePath, TestSign, MSBuildPath, LogDir, enclosingDir, SNBinaryPath);
            var signTool = DryRun ? new ValidationOnlySignTool(signToolArgs, Log) : (SignTool)new RealSignTool(signToolArgs, Log);
            var configuration = new Configuration(TempDir, ItemsToSign, strongNameInfo, fileSignInfo, extensionSignInfo, dualCertificates, Log, useHashInExtractionPath: UseHashInExtractionPath);

            if (ReadExistingContainerSigningCache)
            {
                configuration.ReadExistingContainerSigningCache();
            }

            var signingInput = configuration.GenerateListOfFiles();

            if (Log.HasLoggedErrors) return;

            var util = new BatchSignUtil(BuildEngine, Log, signTool, signingInput, ItemsToSkipStrongNameCheck);

            util.SkipZipContainerSignatureMarkerCheck = this.SkipZipContainerSignatureMarkerCheck;

            if (Log.HasLoggedErrors) return;

            util.Go(DoStrongNameCheck);
        }

        private void PrintConfigInformation()
        {
            Log.LogMessage(MessageImportance.High, "SignToolTask starting.");
            Log.LogMessage(MessageImportance.High, $"DryRun: {DryRun}");
            Log.LogMessage(MessageImportance.High, $"Signing mode: { (TestSign ? "Test" : "Real") }");
            Log.LogMessage(MessageImportance.High, $"MicroBuild signing logs will be in (Signing*.binlog): {LogDir}");
            Log.LogMessage(MessageImportance.High, $"MicroBuild signing configuration will be in (Round*.proj): {TempDir}");
        }

        private string[] ParseCertificateInfo()
        {
            var dualCertificates = CertificatesSignInfo?
                .Where(item => item.GetMetadata("DualSigningAllowed").Equals("true", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.ItemSpec);

            return dualCertificates?.ToArray();
        }

        private string GetEnclosingDirectoryOfItemsToSign()
        {
            var separators = new[] { '/', '\\' };
            string[] result = null;

            if (ItemsToSign.Length == 0)
            {
                return string.Empty;
            }

            foreach (var path in ItemsToSign)
            {
                if (!Path.IsPathRooted(path))
                {
                    Log.LogError($"Paths specified in {nameof(ItemsToSign)} must be absolute: '{path}'.");
                    continue;
                }

                var directoryParts = Path.GetFullPath(Path.GetDirectoryName(path)).Split(separators);
                if (result == null)
                {
                    result = directoryParts;
                    continue;
                }

                Array.Resize(ref result, getCommonPrefixLength(result, directoryParts));
            }

            if (result == null || result.Length == 0)
            {
                Log.LogError($"All {nameof(ItemsToSign)} must be within the cone of a single directory.");
                return string.Empty;
            }

            return string.Join(Path.DirectorySeparatorChar.ToString(), result);

            int getCommonPrefixLength(string[] dir1, string[] dir2)
            {
                int min = Math.Min(dir1.Length, dir2.Length);

                for (int i = 0; i < min; i++)
                {
                    if (dir1[i] != dir2[i])
                    {
                        return i;
                    }
                }

                return min;
            }
        }

        private Dictionary<string, SignInfo> ParseFileExtensionSignInfo()
        {
            var map = new Dictionary<string, SignInfo>(StringComparer.OrdinalIgnoreCase);

            if (FileExtensionSignInfo != null)
            {
                foreach (var item in FileExtensionSignInfo)
                {
                    var extension = item.ItemSpec;
                    var certificate = item.GetMetadata("CertificateName");

                    if (!extension.Equals(Path.GetExtension(extension)))
                    {
                        Log.LogError($"Value of {nameof(FileExtensionSignInfo)} is invalid: '{extension}'");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(certificate))
                    {
                        Log.LogError($"CertificateName metadata of {nameof(FileExtensionSignInfo)} is invalid: '{certificate}'");
                        continue;
                    }

                    if (map.ContainsKey(extension))
                    {
                        Log.LogWarning($"Duplicated signing information for extension: {extension}. Overriding the previous entry.");
                    }

                    map[extension] = certificate.Equals(SignToolConstants.IgnoreFileCertificateSentinel, StringComparison.InvariantCultureIgnoreCase) ?
                        SignInfo.Ignore :
                        new SignInfo(certificate);
                }
            }

            return map;
        }

        private Dictionary<string, SignInfo> ParseStrongNameSignInfo()
        {
            var map = new Dictionary<string, SignInfo>(StringComparer.OrdinalIgnoreCase);

            if (StrongNameSignInfo != null)
            {
                foreach (var item in StrongNameSignInfo)
                {
                    var strongName = item.ItemSpec;
                    var publicKeyToken = item.GetMetadata("PublicKeyToken");
                    var certificateName = item.GetMetadata("CertificateName");

                    if (string.IsNullOrWhiteSpace(strongName))
                    {
                        Log.LogError($"An invalid strong name was specified in {nameof(StrongNameSignInfo)}: '{strongName}'");
                        continue;
                    }

                    if (!IsValidPublicKeyToken(publicKeyToken))
                    {
                        Log.LogError($"PublicKeyToken metadata of {nameof(StrongNameSignInfo)} is invalid: '{publicKeyToken}'");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(certificateName))
                    {
                        Log.LogMessage($"CertificateName metadata of {nameof(StrongNameSignInfo)} is invalid or empty: '{certificateName}'");
                        continue;
                    }

                    if (SignToolConstants.IgnoreFileCertificateSentinel.Equals(certificateName, StringComparison.OrdinalIgnoreCase) &&
                        SignToolConstants.IgnoreFileCertificateSentinel.Equals(strongName, StringComparison.OrdinalIgnoreCase))
                    {
                        Log.LogWarning($"CertificateName & ItemSpec metadata of {nameof(StrongNameSignInfo)} shouldn't be both '{SignToolConstants.IgnoreFileCertificateSentinel}'");
                        continue;
                    }

                    var signInfo = SignToolConstants.IgnoreFileCertificateSentinel.Equals(strongName, StringComparison.OrdinalIgnoreCase)
                        ? new SignInfo(certificateName)
                        : new SignInfo(certificateName, strongName);

                    if (map.ContainsKey(publicKeyToken))
                    {
                        Log.LogError($"Duplicate entries in {nameof(StrongNameSignInfo)} with the same key '{publicKeyToken}'.");
                        continue;
                    }

                    map.Add(publicKeyToken, signInfo);
                }
            }

            return map;
        }

        private Dictionary<ExplicitCertificateKey, string> ParseFileSignInfo()
        {
            var map = new Dictionary<ExplicitCertificateKey, string>();

            if (FileSignInfo != null)
            {
                foreach (var item in FileSignInfo)
                {
                    var fileName = item.ItemSpec;
                    var targetFramework = item.GetMetadata("TargetFramework");
                    var publicKeyToken = item.GetMetadata("PublicKeyToken");
                    var certificateName = item.GetMetadata("CertificateName");

                    if (fileName.IndexOfAny(new[] {'/', '\\'}) >= 0)
                    {
                        Log.LogError($"{nameof(FileSignInfo)} should specify file name and extension, not a full path: '{fileName}'");
                        continue;
                    }

                    if (!string.IsNullOrEmpty(targetFramework) && !IsValidTargetFrameworkName(targetFramework))
                    {
                        Log.LogError($"TargetFramework metadata of {nameof(FileSignInfo)} is invalid: '{targetFramework}'");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(certificateName))
                    {
                        Log.LogError($"CertificateName metadata of {nameof(FileSignInfo)} is invalid: '{certificateName}'");
                        continue;
                    }

                    if (!string.IsNullOrEmpty(publicKeyToken) && !IsValidPublicKeyToken(publicKeyToken))
                    {
                        Log.LogError($"PublicKeyToken metadata for {nameof(FileSignInfo)} is invalid: '{publicKeyToken}'");
                        continue;
                    }

                    var key = new ExplicitCertificateKey(fileName, publicKeyToken, targetFramework);
                    if (map.TryGetValue(key, out var existingCert))
                    {
                        Log.LogError($"Duplicate entries in {nameof(FileSignInfo)} with the same key ('{fileName}', '{publicKeyToken}', '{targetFramework}'): '{existingCert}', '{certificateName}'.");
                        continue;
                    }

                    map.Add(key, certificateName);
                }
            }

            return map;
        }

        private bool IsValidTargetFrameworkName(string tfn)
        {
            try
            {
                new FrameworkName(tfn);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool IsValidPublicKeyToken(string pkt)
        {
            if (pkt == null) return false;

            if (pkt.Length != 16) return false;

            return pkt.ToLower().All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z')); ;
        }
    }
}
