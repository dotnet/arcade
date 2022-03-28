// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Workloads.Wix;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Msi
{
    /// <summary>
    /// Represents a workload manifest MSI.
    /// </summary>
    internal class WorkloadManifestMsi : MsiBase
    {
        private WorkloadManifestPackage _package;

        /// <summary>
        /// The directory reference to use when harvesting the package contents.
        /// </summary>
        private static readonly string ManifestIdDirectory = "ManifestIdDir";

        public WorkloadManifestMsi(WorkloadManifestPackage package, string platform, IBuildEngine buildEngine, string wixToolsetPath,
            string baseIntermediateOutputPath) : 
            base(package, buildEngine, wixToolsetPath, platform, baseIntermediateOutputPath)
        {
            _package = package;
        }

        /// <inheritdoc />
        /// <exception cref="Exception" />
        public override ITaskItem Build(string outputPath, ITaskItem[]? iceSuppressions = null)
        {
            // Harvest the package contents before adding it to the source files we need to compile.
            string packageContentWxs = Path.Combine(WixSourceDirectory, "PackageContent.wxs");
            string packageDataDirectory = Path.Combine(_package.DestinationDirectory, "data");

            HarvesterToolTask heat = new(BuildEngine, WixToolsetPath)
            {
                DirectoryReference = ManifestIdDirectory,
                OutputFile = packageContentWxs,
                Platform = this.Platform,
                SourceDirectory = packageDataDirectory
            };

            if (!heat.Execute())
            {
                throw new Exception(Strings.HeatFailedToHarvest);
            }

            CompilerToolTask candle = CreateDefaultCompiler();
            candle.AddSourceFiles(packageContentWxs,
                EmbeddedTemplates.Extract("DependencyProvider.wxs", WixSourceDirectory),
                EmbeddedTemplates.Extract("dotnethome_x64.wxs", WixSourceDirectory),
                EmbeddedTemplates.Extract("ManifestProduct.wxs", WixSourceDirectory));

            // Only extract the include file as it's not compilable, but imported by various source files.
            EmbeddedTemplates.Extract("Variables.wxi", WixSourceDirectory);

            // To support upgrades, the UpgradeCode must be stable within an SDK feature band.
            // For example, 6.0.101 and 6.0.108 will generate the same GUID for the same platform and manifest ID. 
            // The workload author will need to guarantee that the version for the MSI is higher than previous shipped versions
            // to ensure upgrades correctly trigger.
            Guid upgradeCode = Utils.CreateUuid(UpgradeCodeNamespaceUuid, $"{_package.ManifestId};{_package.SdkFeatureBand};{Platform}");
            string providerKeyName = $"{_package.ManifestId},{_package.SdkFeatureBand},{Platform}";

            // Set up additional preprocessor definitions.
            candle.AddPreprocessorDefinition(PreprocessorDefinitionNames.UpgradeCode, $"{upgradeCode}");
            candle.AddPreprocessorDefinition(PreprocessorDefinitionNames.DependencyProviderKeyName, $"{providerKeyName}");
            candle.AddPreprocessorDefinition(PreprocessorDefinitionNames.SourceDir, $"{packageDataDirectory}");
            candle.AddPreprocessorDefinition(PreprocessorDefinitionNames.SdkFeatureBandVersion, $"{_package.SdkFeatureBand}");

            // The temporary installer in the SDK (6.0) used lower invariants of the manifest ID.
            // We have to do the same to ensure the keypath generation produces stable GUIDs.
            candle.AddPreprocessorDefinition(PreprocessorDefinitionNames.ManifestId, $"{_package.ManifestId.ToLowerInvariant()}");

            if (!candle.Execute())
            {
                throw new Exception(Strings.FailedToCompileMsi);
            }

            ITaskItem msi = Link(candle.OutputPath, 
                Path.Combine(outputPath, Path.GetFileNameWithoutExtension(_package.PackagePath) + $"-{Platform}.msi"),
                iceSuppressions);
                        
            return msi;
        }       
    }
}

#nullable disable
