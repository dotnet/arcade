// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.SignTool
{
    public class SignToolTask : Task
    {
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

        /// <summary>
        /// The URL of the feed where the package will be published.
        /// </summary>
        public string PublishUrl { get; set; }

        /// <summary>
        /// Path to where to store a manifest file containing the list of files that WOULD be signed and their respective signing information.
        /// </summary>
        public string OrchestrationManifestPath { get; set; }

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
                    return ;
                }

                if (String.IsNullOrEmpty(LogDir) || !Directory.Exists(LogDir))
                {
                    Log.LogError($"Invalid LogDir informed: {LogDir}");
                    return ;
                }
            }

            var signInfos = ParseStrongNameSignInfo();
            var overridingSignInfos = ParseFileSignInfo();

            if (Log.HasLoggedErrors) return ;

            var signToolArgs = new SignToolArgs(TempDir, MicroBuildCorePath, TestSign, MSBuildPath, LogDir);
            var signTool = DryRun ? new ValidationOnlySignTool(signToolArgs) : (SignTool)new RealSignTool(signToolArgs);
            var signingInput = new BatchSignInput(TempDir, ItemsToSign, signInfos, overridingSignInfos, PublishUrl, Log);

            if (Log.HasLoggedErrors) return ;

            var util = new BatchSignUtil(BuildEngine, Log, signTool, signingInput, OrchestrationManifestPath);

            if (Log.HasLoggedErrors) return ;

            util.Go();
        }

        private Dictionary<string, SignInfo> ParseStrongNameSignInfo()
        {
            var mapTokenToSignInfo = new Dictionary<string, SignInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in StrongNameSignInfo)
            {
                var strongName = item.ItemSpec;
                var publicKeyToken = item.GetMetadata("PublicKeyToken");
                var certificateName = item.GetMetadata("CertificateName");

                if (String.IsNullOrWhiteSpace(strongName))
                {
                    Log.LogError($"An invalid strong name was informed in StrongNameSignInfo: {strongName}");
                    return null;
                }

                if (!IsValidPublicKeyToken(publicKeyToken))
                {
                    Log.LogError($"This PublicKeyToken metadata for StrongNameSignInfo isn't valid: {publicKeyToken}");
                    return null;
                }

                if (String.IsNullOrWhiteSpace(certificateName))
                {
                    Log.LogError($"This CertificateName informed for FileSignInfo isn't valid: {certificateName}");
                    return null;
                }

                var signInfo = new SignInfo(certificateName, strongName);

                mapTokenToSignInfo.Add(publicKeyToken, signInfo);
            }

            return mapTokenToSignInfo;
        }

        private Dictionary<(string fileName, string publicKeyToken, string targetFramework), string> ParseFileSignInfo()
        {
            var mapOverridingSignInfos = new Dictionary<(string, string, string), string>();

            if (FileSignInfo != null)
            {
                foreach (var item in FileSignInfo)
                {
                    var fileName = item.ItemSpec;
                    var targetFramework = item.GetMetadata("TargetFramework");
                    var publicKeyToken = item.GetMetadata("PublicKeyToken");
                    var certificateName = item.GetMetadata("CertificateName");

                    if (fileName.IndexOfAny(new char[]{'/', '\\'}) >= 0)
                    {
                        Log.LogError($"FileSignInfo should include only file name and extension, not the full path to the file: {fileName}");
                        return null;
                    }

                    if (String.IsNullOrEmpty(targetFramework))
                    {
                        targetFramework = SignToolConstants.AllTargetFrameworksSentinel;
                    }
                    else if (!IsValidTargetFrameworkName(targetFramework))
                    {
                        Log.LogError($"This TargetFramework metadata for FileSignInfo isn't valid: {targetFramework}");
                        return null;
                    }

                    if (String.IsNullOrWhiteSpace(certificateName))
                    {
                        Log.LogError($"This CertificateName informed for FileSignInfo isn't valid: {certificateName}");
                        return null;
                    }

                    if (!IsValidPublicKeyToken(publicKeyToken))
                    {
                        Log.LogError($"This PublicKeyToken metadata for FileSignInfo isn't valid: {publicKeyToken}");
                        return null;
                    }

                    var outerKey = (fileName, publicKeyToken.ToLower(), targetFramework);

                    if (mapOverridingSignInfos.TryGetValue(outerKey, out var existingCert))
                    {
                        Log.LogWarning($"Ignoring attempt to duplicate signing override information for this combination ({fileName}, {publicKeyToken}, {targetFramework}). " +
                            $"Existing value {existingCert}, trying to add new value {certificateName}.");
                    }
                    else
                    {
                        mapOverridingSignInfos.Add(outerKey, certificateName);
                    }
                }
            }

            return mapOverridingSignInfos;
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

            return long.TryParse(pkt, out _);
        }
    }
}
