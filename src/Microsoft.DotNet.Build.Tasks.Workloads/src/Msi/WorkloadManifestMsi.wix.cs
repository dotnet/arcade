// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Workloads.Wix;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Msi
{
    /// <summary>
    /// Represents a workload manifest MSI.
    /// </summary>
    internal class WorkloadManifestMsi : MsiBase
    {
        public WorkloadManifestPackage Package { get; }

        public List<WorkloadPackGroupJson> WorkloadPackGroups { get; } = new();

        /// <inheritdoc />
        protected override string BaseOutputName => Path.GetFileNameWithoutExtension(Package.PackagePath);

        protected override string? MsiPackageType => DefaultValues.ManifestMsi;

        /// <summary>
        /// <see langword="true">True</see> if the manifest installer supports side-by-side installs, otherwise it's 
        /// assumed the installer supports major upgrades.
        /// </summary>
        /// <remarks>
        /// Major upgrades require both the ProductVersion and ProductCode to change. Refer to the 
        /// <a href="https://learn.microsoft.com/en-us/windows/win32/msi/major-upgrades">Windows Installer</a> for
        /// more details
        /// </remarks>
        protected bool AllowSideBySideInstalls
        {
            get;
        }

        // To support upgrades, the UpgradeCode must be stable within an SDK feature band.
        // For example, 6.0.101 and 6.0.108 will generate the same GUID for the same platform and manifest ID. 
        // The workload author must ensure that the ProductVersion is higher than previously shipped versions.
        // For SxS installs the UpgradeCode can be a random GUID.
        protected override Guid UpgradeCode =>
            AllowSideBySideInstalls ? Guid.NewGuid() :
                    Utils.CreateUuid(UpgradeCodeNamespaceUuid, $"{Package.ManifestId};{Package.SdkFeatureBand};{Platform}");

        protected override string ProviderKeyName =>
            AllowSideBySideInstalls ? $"{Package.ManifestId},{Package.SdkFeatureBand},{Package.PackageVersion},{Platform}" :
                $"{Package.ManifestId},{Package.SdkFeatureBand},{Platform}";

        protected override string? InstallationRecordKey => "InstalledManifests";

        /// <summary>
        /// Creates a new <see cref="WorkloadManifestMsi"/> instance.
        /// </summary>
        /// <param name="package">The NuGet package containing the workload manifest.</param>
        /// <param name="platform">The target platform of the installer.</param>
        /// <param name="buildEngine"></param>
        /// <param name="baseIntermediateOutputPath"></param>
        /// <param name="allowSideBySideInstalls">Determines whether manifest installers are side-by-side for an SDK feature band or support major upgrades.</param>
        /// <param name="wixToolsetVersion">The version of the WiX toolset to use for building the installer.</param>
        /// <param name="overridePackageVersions">Determines if VersionOverride attributes are generated for package references.</param>
        /// <param name="generateWixpack">Determines if a wixpack archive should be generated. The wixpack is required to sign MSIs using Arcade.</param>
        /// <param name="wixpackOutputDirectory">The directory to use for generating a wixpack for signing.</param>
        public WorkloadManifestMsi(WorkloadManifestPackage package, string platform, IBuildEngine buildEngine,
            string baseIntermediateOutputPath, bool allowSideBySideInstalls = false, string wixToolsetVersion = ToolsetInfo.MicrosoftWixToolsetVersion,
            bool overridePackageVersions = false, bool generateWixpack = false, string? wixpackOutputDirectory = null) :
            base(MsiMetadata.Create(package), buildEngine, platform, baseIntermediateOutputPath, wixToolsetVersion,
                overridePackageVersions, generateWixpack, wixpackOutputDirectory)
        {
            Package = package;
            AllowSideBySideInstalls = allowSideBySideInstalls;
        }

        /// <summary>
        /// Creates a new WiX project for building a workload manifest installer (MSI).
        /// </summary>
        protected override WixProject CreateProject()
        {
            WixProject wixproj = base.CreateProject();

            // Add source files
            EmbeddedTemplates.Extract("ManifestProduct.wxs", WixSourceDirectory);
            EmbeddedTemplates.Extract("DependencyProvider.wxs", WixSourceDirectory);
            EmbeddedTemplates.Extract("dotnethome_x64.wxs", WixSourceDirectory);
            EmbeddedTemplates.Extract("Directories.wxs", WixSourceDirectory);
            EmbeddedTemplates.Extract("Registry.wxs", WixSourceDirectory);

            // Configure file harvesting.
            string packageDataDirectory = Path.Combine(Package.DestinationDirectory, "data");
            string filesWxs = EmbeddedTemplates.Extract("Files.wxs", WixSourceDirectory);

            Utils.StringReplace(filesWxs, Encoding.UTF8,
                (MsiTokens.__DIR_ID__, AllowSideBySideInstalls ? MsiDirectories.ManifestVersionDirectory : MsiDirectories.ManifestIdDirectory),
                (MsiTokens.__COMPONENT_GROUP_ID__, "CG_PackageContents"),
                (MsiTokens.__INCLUDE__, packageDataDirectory + Path.DirectorySeparatorChar + "**"));

            //// Configure harvesting of the manifest package contents.
            //string wixProjectPath = Path.Combine(WixSourceDirectory, "manifest.wixproj");
            
            //wixproj.AddHarvestDirectory(packageDataDirectory,
            //    AllowSideBySideInstalls ? MsiDirectories.ManifestVersionDirectory : MsiDirectories.ManifestIdDirectory,
            //    PreprocessorDefinitionNames.SourceDir);

            foreach (var file in Directory.GetFiles(packageDataDirectory).Select(f => Path.GetFullPath(f)))
            {
                NuGetPackageFiles[file] = @"\data\extractedManifest\" + Path.GetFileName(file);
            }

            // Add WorkloadPackGroups.json to add to workload manifest MSI
            string? jsonContentWxs = null;
            string? jsonDirectory = null;

            // Default the variable to false. If we harvested workload pack group data, we'll override it
            wixproj.AddPreprocessorDefinition("IncludePackGroupJson", "false");

            if (WorkloadPackGroups.Any())
            {
                jsonContentWxs = Path.Combine(WixSourceDirectory, "JsonContent.wxs");

                string jsonAsString = JsonSerializer.Serialize(WorkloadPackGroups, typeof(IList<WorkloadPackGroupJson>), new JsonSerializerOptions() { WriteIndented = true });
                jsonDirectory = Path.Combine(WixSourceDirectory, "json");
                Directory.CreateDirectory(jsonDirectory);

                string jsonFullPath = Path.GetFullPath(Path.Combine(jsonDirectory, "WorkloadPackGroups.json"));
                File.WriteAllText(jsonFullPath, jsonAsString);

                wixproj.AddHarvestDirectory(jsonDirectory,
                    AllowSideBySideInstalls ? MsiDirectories.ManifestVersionDirectory : MsiDirectories.ManifestIdDirectory,
                    "JsonSourceDir",
                    "CG_PackGroupJson");

                wixproj.AddPreprocessorDefinition("IncludePackGroupJson", "true");
                wixproj.AddPreprocessorDefinition("JsonSourceDir", jsonDirectory);

                NuGetPackageFiles[jsonFullPath] = @"\data\extractedManifest\" + Path.GetFileName(jsonFullPath);
            }

            // Add preprocessor definitions
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.SourceDir, $"{packageDataDirectory}");
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.SdkFeatureBandVersion, $"{Package.SdkFeatureBand}");

            // The temporary installer in the SDK (6.0) used lower invariants of the manifest ID.
            // We have to do the same to ensure the keypath generation produces stable GUIDs.
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.ManifestId, $"{Package.ManifestId.ToLowerInvariant()}");

            if (AllowSideBySideInstalls)
            {
                wixproj.AddPreprocessorDefinition("ManifestVersion", Package.GetManifest().Version);
            }

            return wixproj;
        }

        public class WorkloadPackGroupJson
        {
            public string? GroupPackageId { get; set; }
            public string? GroupPackageVersion { get; set; }

            public List<WorkloadPackJson> Packs { get; set; } = new List<WorkloadPackJson>();
        }

        public class WorkloadPackJson
        {
            public string? PackId { get; set; }

            public string? PackVersion { get; set; }
        }
    }
}

#nullable disable
