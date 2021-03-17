// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{

    public class GenerateVisualStudioMsiPackageProject : GenerateTaskBase
    {
        public string Chip
        {
            get;
            set;
        }

        public string MsiPath
        {
            get;
            set;
        }

        /// <summary>
        /// The name of the Visual Studio package, e.g. Microsoft.VisualStudio.Foo
        /// </summary>
        [Required]
        public string PackageName
        {
            get;
            set;
        }

        public Version Version
        {
            get;
            set;
        }

        internal long PayloadSize
        {
            get;
            set;
        }

        internal long InstallSize
        {
            get;
            set;
        }

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

                string swixSourceDirectory = Path.Combine(SourceDirectory, Utils.GetHash(MsiPath, "MD5"));
                string msiSwr = EmbeddedTemplates.Extract("msi.swr", swixSourceDirectory);
                string msiSwixProj = EmbeddedTemplates.Extract("msi.swixproj", swixSourceDirectory, PackageName+".swixproj");

                FileInfo msiInfo = new (MsiPath);
                PayloadSize = msiInfo.Length;
                InstallSize = MsiUtils.GetInstallSize(MsiPath);
                Log.LogMessage($"MSI payload size: {PayloadSize}, install size (estimated): {InstallSize} ");

                Utils.StringReplace(msiSwr, GetReplacementTokens(), Encoding.UTF8);
                Utils.StringReplace(msiSwixProj, GetReplacementTokens(), Encoding.UTF8);
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
                {"__VS_PAYLOAD_SOURCE__", MsiPath },
                {"__VS_PAYLOAD_SIZE__", $"{PayloadSize}" },
            };
        }
    }
}
