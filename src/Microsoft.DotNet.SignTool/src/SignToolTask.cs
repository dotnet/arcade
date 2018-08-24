// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.SignTool
{
    public class SignToolTask : Task
    {
#if NET461
        static SignToolTask() => AssemblyResolution.Initialize();
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
        /// Mapping relating PublicKeyToken, CertificateName and Strong Name. 
        /// Metadata required: PublicKeyToken, CertificateName and Include (which will be the Strong Name)
        /// During signing Certificate and Strong Name will be looked up here based on PublicKeyToken.
        /// </summary>
        [Required]
        public ITaskItem[] StrongNameSignInfo { get; set; }

        /// <summary>
        /// Let the user override the default certificate used for Signing.
        /// Metadata required: CertificateName, TargetFramework, Include (which is the name of the file+extension to be signed)
        /// </summary>
        public ITaskItem[] FileSignInfo { get; set; }

        /// <summary>
        /// Path to msbuild.exe. Required if <see cref="DryRun"/> is <c>false</c>.
        /// </summary>
        public string MSBuildPath { get; set; }

        /// <summary>
        /// Directory to write log to. Required if <see cref="DryRun"/> is <c>false</c>.
        /// </summary>
        public string LogDir { get; set; }

        public override bool Execute()
        {
            ExecuteImpl();
            return !Log.HasLoggedErrors;
        }

        public void ExecuteImpl()
        {
            if (!DryRun)
            {
                if (typeof(object).Assembly.GetName().Name != "mscorlib" && !File.Exists(MSBuildPath))
                {
                    Log.LogError($"MSBuild was not found at this path: '{MSBuildPath}'.");
                    return;
                }

                if (string.IsNullOrEmpty(LogDir) || !Directory.Exists(LogDir))
                {
                    Log.LogError($"Invalid LogDir informed: {LogDir}");
                    return;
                }
            }

            var defaultSignInfoForPublicKeyToken = ParseStrongNameSignInfo();
            var explicitCertificates = ParseFileSignInfo();

            if (Log.HasLoggedErrors) return;

            var signToolArgs = new SignToolArgs(TempDir, MicroBuildCorePath, TestSign, MSBuildPath, LogDir);
            var signTool = DryRun ? new ValidationOnlySignTool(signToolArgs) : (SignTool)new RealSignTool(signToolArgs);
            var signingInput = new Configuration(TempDir, ItemsToSign, defaultSignInfoForPublicKeyToken, explicitCertificates, Log).GenerateListOfFiles();

            if (Log.HasLoggedErrors) return;

            var util = new BatchSignUtil(BuildEngine, Log, signTool, signingInput);

            if (Log.HasLoggedErrors) return;

            util.Go();
        }

        private Dictionary<string, SignInfo> ParseStrongNameSignInfo()
        {
            var map = new Dictionary<string, SignInfo>(StringComparer.OrdinalIgnoreCase);

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
                    Log.LogError($"CertificateName metadata of {nameof(StrongNameSignInfo)} is invalid: '{certificateName}'");
                    continue;
                }

                var signInfo = new SignInfo(certificateName, strongName);

                if (map.ContainsKey(publicKeyToken))
                {
                    Log.LogError($"Duplicate entries in {nameof(StrongNameSignInfo)} with the same key '{publicKeyToken}'.");
                    continue;
                }

                map.Add(publicKeyToken, signInfo);
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
