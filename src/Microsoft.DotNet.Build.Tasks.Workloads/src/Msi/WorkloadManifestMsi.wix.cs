// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Workloads.Wix;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Msi
{
    /// <summary>
    /// Represents a workload manifest MSI.
    /// </summary>
    internal class WorkloadManifestMsi : MsiBase
    {
        public WorkloadManifestPackage Package { get; }

        /// <summary>
        /// The directory reference to use when harvesting the package contents.
        /// </summary>
        private static readonly string ManifestIdDirectory = "ManifestIdDir";

        public List<WorkloadPackGroupPackage> WorkloadPackGroups { get; } = new();

        public WorkloadManifestMsi(WorkloadManifestPackage package, string platform, IBuildEngine buildEngine, string wixToolsetPath,
            string baseIntermediateOutputPath) : 
            base(MsiMetadata.Create(package), buildEngine, wixToolsetPath, platform, baseIntermediateOutputPath)
        {
            Package = package;
        }

        /// <inheritdoc />
        /// <exception cref="Exception" />
        public override ITaskItem Build(string outputPath, ITaskItem[]? iceSuppressions = null)
        {
            // Harvest the package contents before adding it to the source files we need to compile.
            string packageContentWxs = Path.Combine(WixSourceDirectory, "PackageContent.wxs");
            string packageDataDirectory = Path.Combine(Package.DestinationDirectory, "data");

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

            //  Add WorkloadPackGroups.json to add to workload manifest MSI
            string? jsonContentWxs = null;
            string? jsonDirectory = null;

            if (WorkloadPackGroups.Any())
            {
                jsonContentWxs = Path.Combine(WixSourceDirectory, "JsonContent.wxs");

                List<WorkloadPackGroupJson> packGroupListJson = new List<WorkloadPackGroupJson>();
                foreach (var packGroup in WorkloadPackGroups)
                {
                    var json = new WorkloadPackGroupJson()
                    {
                        GroupPackageId = packGroup.Id,
                        GroupPackageVersion = packGroup.GetMsiMetadata().PackageVersion.ToString()
                    };
                    json.Packs.AddRange(packGroup.Packs.Select(p => new WorkloadPackJson()
                    {
                        PackId = p.Id,
                        PackVersion = p.PackageVersion.ToString()
                    }));

                    packGroupListJson.Add(json);
                }

                string jsonAsString = JsonSerializer.Serialize(packGroupListJson, typeof(IList<WorkloadPackGroupJson>), new JsonSerializerOptions() { WriteIndented = true });
                jsonDirectory = Path.Combine(WixSourceDirectory, "json");
                Directory.CreateDirectory(jsonDirectory);
                File.WriteAllText(Path.Combine(jsonDirectory, "WorkloadPackGroups.json"), jsonAsString);

                HarvesterToolTask jsonHeat = new(BuildEngine, WixToolsetPath)
                {
                    DirectoryReference = ManifestIdDirectory,
                    OutputFile = jsonContentWxs,
                    Platform = this.Platform,
                    SourceDirectory = jsonDirectory,
                    SourceVariableName = "JsonSourceDir",
                    ComponentGroupName = "CG_PackGroupJson"
                };

                if (!jsonHeat.Execute())
                {
                    throw new Exception(Strings.HeatFailedToHarvest);
                }
            }

            CompilerToolTask candle = CreateDefaultCompiler();
            candle.AddSourceFiles(packageContentWxs,
                EmbeddedTemplates.Extract("DependencyProvider.wxs", WixSourceDirectory),
                EmbeddedTemplates.Extract("dotnethome_x64.wxs", WixSourceDirectory),
                EmbeddedTemplates.Extract("ManifestProduct.wxs", WixSourceDirectory));

            if (jsonContentWxs != null)
            {
                candle.AddSourceFiles(jsonContentWxs);
                candle.AddPreprocessorDefinition("IncludePackGroupJson", "true");
                candle.AddPreprocessorDefinition("JsonSourceDir", jsonDirectory);
            }
            else
            {
                candle.AddPreprocessorDefinition("IncludePackGroupJson", "false");
            }

            // Only extract the include file as it's not compilable, but imported by various source files.
            EmbeddedTemplates.Extract("Variables.wxi", WixSourceDirectory);

            // To support upgrades, the UpgradeCode must be stable within an SDK feature band.
            // For example, 6.0.101 and 6.0.108 will generate the same GUID for the same platform and manifest ID. 
            // The workload author will need to guarantee that the version for the MSI is higher than previous shipped versions
            // to ensure upgrades correctly trigger.
            Guid upgradeCode = Utils.CreateUuid(UpgradeCodeNamespaceUuid, $"{Package.ManifestId};{Package.SdkFeatureBand};{Platform}");
            string providerKeyName = $"{Package.ManifestId},{Package.SdkFeatureBand},{Platform}";

            // Set up additional preprocessor definitions.
            candle.AddPreprocessorDefinition(PreprocessorDefinitionNames.UpgradeCode, $"{upgradeCode:B}");
            candle.AddPreprocessorDefinition(PreprocessorDefinitionNames.DependencyProviderKeyName, $"{providerKeyName}");
            candle.AddPreprocessorDefinition(PreprocessorDefinitionNames.SourceDir, $"{packageDataDirectory}");
            candle.AddPreprocessorDefinition(PreprocessorDefinitionNames.SdkFeatureBandVersion, $"{Package.SdkFeatureBand}");

            // The temporary installer in the SDK (6.0) used lower invariants of the manifest ID.
            // We have to do the same to ensure the keypath generation produces stable GUIDs.
            candle.AddPreprocessorDefinition(PreprocessorDefinitionNames.ManifestId, $"{Package.ManifestId.ToLowerInvariant()}");

            if (!candle.Execute())
            {
                throw new Exception(Strings.FailedToCompileMsi);
            }

            ITaskItem msi = Link(candle.OutputPath, 
                Path.Combine(outputPath, Path.GetFileNameWithoutExtension(Package.PackagePath) + $"-{Platform}.msi"),
                iceSuppressions);
                        
            return msi;
        }


        class WorkloadPackGroupJson
        {
            public string? GroupPackageId { get; set; }
            public string? GroupPackageVersion { get; set; }

            public List<WorkloadPackJson> Packs { get; set; } = new List<WorkloadPackJson>();
        }

        class WorkloadPackJson
        {
            public string? PackId { get; set; }

            public string? PackVersion { get; set; }
        }
    }
}

#nullable disable
