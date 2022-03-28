// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Msi
{
    /// <summary>
    /// Describes a project to package an MSI and its JSON manifest into a NuGet package.
    /// </summary>
    internal class MsiPayloadPackageProject : ProjectTemplateBase
    {
        /// <inheritdoc />
        protected override string ProjectSourceDirectory
        {
            get;
        }

        /// <inheritdoc />
        protected override string ProjectFile
        {
            get;
        }

        public MsiPayloadPackageProject(WorkloadPackageBase package, ITaskItem msi, string baseIntermediateOutputPath, string baseOutputPath, string msiJsonPath) :
            base(baseIntermediateOutputPath, baseOutputPath)
        {
            string platform = msi.GetMetadata(Metadata.Platform);
            ProjectSourceDirectory = Path.Combine(SourceDirectory, "msiPackage", platform, package.Id);
            ProjectFile = "msi.csproj";

            ReplacementTokens[PayloadPackageTokens.__AUTHORS__] = package.Authors;
            ReplacementTokens[PayloadPackageTokens.__COPYRIGHT__] = package.Copyright;
            ReplacementTokens[PayloadPackageTokens.__DESCRIPTION__] = package.Description;
            ReplacementTokens[PayloadPackageTokens.__PACKAGE_ID__] = $"{package.Id}.Msi.{platform}";
            ReplacementTokens[PayloadPackageTokens.__PACKAGE_PROJECT_URL__] = package.ProjectUrl;
            ReplacementTokens[PayloadPackageTokens.__PACKAGE_VERSION__] = $"{package.PackageVersion}";
            ReplacementTokens[PayloadPackageTokens.__MSI__] = msi.GetMetadata(Metadata.FullPath);
            ReplacementTokens[PayloadPackageTokens.__MSI_JSON__] = msiJsonPath;
            ReplacementTokens[PayloadPackageTokens.__LICENSE_FILENAME__] = "LICENSE.TXT";
        }

        /// <inheritdoc />
        public override string Create()
        {
            string msiCsproj = EmbeddedTemplates.Extract("msi.csproj", ProjectSourceDirectory);

            Utils.StringReplace(msiCsproj, ReplacementTokens, Encoding.UTF8);
            EmbeddedTemplates.Extract("Icon.png", ProjectSourceDirectory);
            EmbeddedTemplates.Extract("LICENSE.TXT", ProjectSourceDirectory);

            return msiCsproj;
        }
    }
}
