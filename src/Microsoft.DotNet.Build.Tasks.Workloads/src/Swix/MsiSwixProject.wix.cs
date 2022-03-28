// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Workloads.Msi;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Swix
{
    /// <summary>
    /// Creates a SWIX project for an MSI package.
    /// </summary>
    public class MsiSwixProject : SwixProjectBase
    {
        /// <summary>
        /// The target platform of the package.
        /// </summary>
        protected string Chip
        {
            get;
        }

        /// <inheritdoc />
        protected override string ProjectFile
        {
            get;
        }

        /// <inheritdoc />
        protected override string ProjectSourceDirectory
        {
            get;
        }

        public MsiSwixProject(ITaskItem msi, string baseIntermediateOutputPath, string baseOutputPath, 
            string visualStudioProductArchitecture = "neutral") : base(msi.GetMetadata(Metadata.SwixPackageId), new Version(msi.GetMetadata(Metadata.Version)), baseIntermediateOutputPath, baseOutputPath)
        {
            Chip = msi.GetMetadata(Metadata.Platform);
            ProjectSourceDirectory = Path.Combine(SwixDirectory, Id, Chip);

            ValidateRelativePackagePath($@"{Id},version={Version},chip={Chip},productarch={visualStudioProductArchitecture}\{Path.GetFileName(msi.ItemSpec)}");

            // The name of the .swixproj file is used to create the JSON manifest that will be merged into the .vsman file later.
            // For drop publishing all the JSON manifests and payloads must reside in the same folder so we shorten the project names
            // and use a hashed filename to avoid path too long errors.
            string projectName = $"{Id}.{Version.ToString(3)}.{Chip}";
            ProjectFile = $"{Utils.GetHash(projectName, HashAlgorithmName.MD5)}.swixproj";

            FileInfo fileInfo = new(msi.ItemSpec);

            ReplacementTokens[SwixTokens.__VS_PACKAGE_CHIP__] = Chip;
            ReplacementTokens[SwixTokens.__VS_PACKAGE_INSTALL_SIZE_SYSTEM_DRIVE__] = $"{MsiUtils.GetInstallSize(msi.ItemSpec)}";
            ReplacementTokens[SwixTokens.__VS_PACKAGE_PRODUCT_ARCH__] = visualStudioProductArchitecture;
            ReplacementTokens[SwixTokens.__VS_PAYLOAD_SIZE__] = $"{fileInfo.Length}";
            ReplacementTokens[SwixTokens.__VS_PAYLOAD_SOURCE__] = msi.GetMetadata(Metadata.FullPath);
        }

        /// <inheritdoc />
        public override string Create()
        {
            string swixProj = EmbeddedTemplates.Extract("msi.swixproj", ProjectSourceDirectory, ProjectFile);

            Utils.StringReplace(swixProj, ReplacementTokens, Encoding.UTF8);
            Utils.StringReplace(EmbeddedTemplates.Extract("msi.swr", ProjectSourceDirectory), ReplacementTokens, Encoding.UTF8);

            return swixProj;
        }
    }
}
