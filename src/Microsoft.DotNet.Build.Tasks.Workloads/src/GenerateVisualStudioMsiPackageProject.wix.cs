// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// MSBuild task for generating a Visual Studio MSI package project (.swixproj).
    /// </summary>
    public class GenerateVisualStudioMsiPackageProject : GenerateTaskBase
    {
        /// <summary>
        /// The OS architecture targeted by the MSI.
        /// </summary>
        [Required]
        public string Chip
        {
            get;
            set;
        }

        /// <summary>
        /// The path of the MSI file.
        /// </summary>
        public string MsiPath
        {
            get;
            set;
        }

        /// <summary>
        /// The name of the Visual Studio package (ID), e.g. "Microsoft.VisualStudio.X.Y.Z", that will wrap the MSI.
        /// </summary>
        [Required]
        public string PackageName
        {
            get;
            set;
        }

        /// <summary>
        /// The version of the MSI payload package in the Visual Studio manifest. 
        /// </summary>
        public Version Version
        {
            get;
            set;
        }

        /// <summary>
        /// The path of the generated .swixproj file.
        /// </summary>
        [Output]
        public string SwixProject
        {
            get;
            set;
        }

        /// <summary>
        /// The size of the MSI in bytes.
        /// </summary>
        internal long PayloadSize
        {
            get;
            set;
        }

        /// <summary>
        /// The size of the installation in bytes. The size is an estimate based on the data in the File table, multiplied
        /// by a factor to account for registry entries in the component database, a.k.a, Darwin descriptors.
        /// </summary>
        internal long InstallSize
        {
            get;
            set;
        }

        /// <summary>
        /// The product architecture to which the package applies, the default is neutral.
        /// </summary>
        internal string ProductArch
        {
            get;
            set;
        } = "neutral";

        public override bool Execute()
        {
            try
            {
                Log.LogMessage($"Generating SWIX package authoring for '{MsiPath}'");

                if (Version == null)
                {
                    // Use the version of the MSI if none was specified
                    Version = new Version(MsiUtils.GetProperty(MsiPath, "ProductVersion"));

                    Log.LogMessage($"Using MSI version for package version: {Version}");
                }

                // Try to catch VS manifest validation errors before we get to VS.
                string vsPayloadRelativePath = $"{PackageName},version={Version.ToString(3)},chip={Chip},productarch={ProductArch}\\{Path.GetFileName(MsiPath)}";
                CheckRelativePayloadPath(vsPayloadRelativePath);

                string swixSourceDirectory = Path.Combine(SwixDirectory, PackageName, Chip);
                string msiSwr = EmbeddedTemplates.Extract("msi.swr", swixSourceDirectory);
                string fullProjectName = $"{PackageName}.{Version.ToString(3)}.{Chip}";
                string msiSwixProj = EmbeddedTemplates.Extract("msi.swixproj", swixSourceDirectory, $"{Utils.GetHash(fullProjectName, "MD5")}.swixproj");

                FileInfo msiInfo = new(MsiPath);
                PayloadSize = msiInfo.Length;
                InstallSize = MsiUtils.GetInstallSize(MsiPath);
                Log.LogMessage($"MSI payload size: {PayloadSize}, install size (estimated): {InstallSize} ");

                Utils.StringReplace(msiSwr, GetReplacementTokens(), Encoding.UTF8);
                Utils.StringReplace(msiSwixProj, GetReplacementTokens(), Encoding.UTF8);

                SwixProject = msiSwixProj;
            }
            catch (Exception e)
            {
                Log.LogMessage(e.StackTrace);
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }

        private Dictionary<string, string> GetReplacementTokens()
        {
            return new Dictionary<string, string>()
            {
                {"__VS_PACKAGE_NAME__", PackageName },
                {"__VS_PACKAGE_VERSION__", Version.ToString() },
                {"__VS_PACKAGE_CHIP__", Chip },
                {"__VS_PACKAGE_INSTALL_SIZE_SYSTEM_DRIVE__", $"{InstallSize}"},
                {"__VS_PACKAGE_PRODUCT_ARCH__", $"{ProductArch}" },
                {"__VS_PAYLOAD_SOURCE__", MsiPath },
                {"__VS_PAYLOAD_SIZE__", $"{PayloadSize}" },
            };
        }
    }
}
