// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Workloads.Wix;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Msi
{
    internal class WorkloadPackMsi : MsiBase
    {
        private WorkloadPackPackage _package;

        /// <inheritdoc />
        protected override string BaseOutputName => _package.ShortName;

        protected override string ProviderKeyName =>
            $"{_package.Id},{_package.PackageVersion},{Platform}";

        protected override Guid UpgradeCode =>
            Utils.CreateUuid(UpgradeCodeNamespaceUuid, $"{_package.Identity};{Platform}");

        protected override string? InstallationRecordKey => "InstalledPacks";

        public WorkloadPackMsi(WorkloadPackPackage package, string platform, IBuildEngine buildEngine, string wixToolsetPath,
            string baseIntermediatOutputPath, string wixToolsetVersion = ToolsetInfo.MicrosoftWixToolsetVersion,
            bool overridePackageVersions = false, bool generateWixPack = false,
            string? wixpackOutputDirectory = null) :
            base(MsiMetadata.Create(package), buildEngine, platform, baseIntermediatOutputPath, wixToolsetVersion,
                overridePackageVersions, generateWixPack, wixpackOutputDirectory)
        {
            _package = package;
        }

        protected override WixProject CreateProject()
        {
            WixProject wixproj = base.CreateProject();
            string wixProjectPath = Path.Combine(WixSourceDirectory, "pack.wixproj");

            EmbeddedTemplates.Extract("Product.wxs", WixSourceDirectory);
            EmbeddedTemplates.Extract("DependencyProvider.wxs", WixSourceDirectory);
            EmbeddedTemplates.Extract("dotnethome_x64.wxs", WixSourceDirectory);
            EmbeddedTemplates.Extract("Directories.wxs", WixSourceDirectory);
            EmbeddedTemplates.Extract("WorkloadPackDirectories.wxs", WixSourceDirectory);
            EmbeddedTemplates.Extract("Registry.wxs", WixSourceDirectory);

            string directoryReference = _package.Kind == WorkloadPackKind.Library || _package.Kind == WorkloadPackKind.Template ?
                "InstallDir" : "VersionDir";

            wixproj.AddHarvestDirectory(_package.DestinationDirectory, directoryReference,
                PreprocessorDefinitionNames.SourceDir);

            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.InstallDir, $"{GetInstallDir(_package.Kind)}");
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.PackKind, $"{_package.Kind}");
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.SourceDir, $"{_package.DestinationDirectory}");

            return wixproj;
        }

        /// <summary>
        /// Gets the name of the installation directory based on the kind of workload pack.
        /// </summary>
        /// <param name="kind">The workload pack kind.</param>
        /// <returns>The name of the root installation directory.</returns>
        internal static string GetInstallDir(WorkloadPackKind kind) =>
            kind switch
            {
                WorkloadPackKind.Framework or WorkloadPackKind.Sdk => "packs",
                WorkloadPackKind.Library => "library-packs",
                WorkloadPackKind.Template => "template-packs",
                WorkloadPackKind.Tool => "tool-packs",
                _ => throw new ArgumentException(string.Format(Strings.UnknownWorkloadKind, kind)),
            };

        /// <summary>
        /// Gets the directory reference (ID) associated with the workload pack kind.
        /// </summary>
        /// <param name="kind">The workload pack kind.</param>
        /// <returns>The directory reference (ID) of the installation directory.</returns>
        internal static string GetDirectoryReference(WorkloadPackKind kind) =>
            kind switch
            {
                WorkloadPackKind.Framework or WorkloadPackKind.Sdk => "PacksDir",
                WorkloadPackKind.Library => "LibraryPacksDir",
                WorkloadPackKind.Template => "TemplatePacksDir",
                WorkloadPackKind.Tool => "ToolPacksDir",
                _ => throw new ArgumentException(string.Format(Strings.UnknownWorkloadKind, kind)),
            };
    }
}

#nullable disable
