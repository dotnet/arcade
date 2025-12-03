// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Workloads.Wix;

using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Msi
{
    internal class WorkloadPackGroupMsi : MsiBase
    {
        WorkloadPackGroupPackage _package;

        int _dirIdCount = 1;

        /// <inheritdoc />
        protected override string BaseOutputName => Metadata.Id;

        protected override string ProviderKeyName =>
            $"{_package.Id},{Metadata.PackageVersion},{Platform}";

        protected override string? InstallationRecordKey => "InstalledPackGroups";

        protected override Guid UpgradeCode =>
            Utils.CreateUuid(UpgradeCodeNamespaceUuid, $"{Metadata.Id};{Platform}");

        /// <summary>
        /// Gets a new directory ID.
        /// </summary>
        protected string DirectoryId => $"dir_{_dirIdCount++:0000}";

        public WorkloadPackGroupMsi(WorkloadPackGroupPackage package, string platform, IBuildEngine buildEngine,
            string baseIntermediatOutputPath, string wixToolsetVersion = ToolsetInfo.MicrosoftWixToolsetVersion,
            bool overridePackageVersions = false, bool generateWixPack = false)
             : base(package.GetMsiMetadata(), buildEngine, platform, baseIntermediatOutputPath, wixToolsetVersion,
                   overridePackageVersions, generateWixPack)
        {
            _package = package;
        }

        protected override WixProject CreateProject()
        {
            WixProject wixproj = base.CreateProject();

            string wixProjectPath = Path.Combine(WixSourceDirectory, "packgroup.wixproj");

            EmbeddedTemplates.Extract("PackDirectories.wxs", WixSourceDirectory);
            EmbeddedTemplates.Extract("Directories.wxs", WixSourceDirectory);
            EmbeddedTemplates.Extract("DependencyProvider.wxs", WixSourceDirectory);
            EmbeddedTemplates.Extract("dotnethome_x64.wxs", WixSourceDirectory);

            // Extract and modify the installation record. For pack groups, we need to add an entry for each
            // workload pack installed by the group.
            string registryWxsPath = EmbeddedTemplates.Extract("Registry.wxs", WixSourceDirectory);
            var registryDoc = XDocument.Load(registryWxsPath);

#nullable disable
            if (registryDoc != null)
            {
                var ns = registryDoc.Root.Name.Namespace;
                var registryKeyElement = registryDoc.Root.Descendants(ns + "RegistryKey").Single();
                foreach (var pack in _package.Packs)
                {
                    registryKeyElement.Add(new XElement(ns + "RegistryKey", new XAttribute("Key", pack.Id),
                        new XElement(ns + "RegistryKey", new XAttribute("Key", pack.PackageVersion),
                        new XElement(ns + "RegistryValue", new XAttribute("Value", ""), new XAttribute("Type", "string")))));
                }
                registryDoc.Save(registryWxsPath);
            }
#nullable enable

            int packNumber = 1;

            HashSet<string> directoryReferences = new();

            foreach (var pack in _package.Packs)
            {
                // wixproj.AddHarvestDirectory(pack.DestinationDirectory,,, $"CG_PackageContents_{packNumber}");

                // Calculate the installation directory for the pack and generate a unique reference
                string packInstallDir = WorkloadPackMsi.GetInstallDir(pack.Kind);
                string packInstallDirReference = WorkloadPackMsi.GetDirectoryReference(pack.Kind);

                if (pack.Kind != WorkloadPackKind.Library && pack.Kind != WorkloadPackKind.Template)
                {
                    // Add directories for the package ID and version under the installation folder.
                    string dirId = DirectoryId;
                    AddDirectory(pack.Id, dirId, packInstallDirReference);
                    packInstallDirReference = DirectoryId;
                    AddDirectory(pack.PackageVersion.ToString(), packInstallDirReference, dirId);
                }

                string sourceDir = $"SourceDir_{packNumber}";
                wixproj.AddHarvestDirectory(pack.DestinationDirectory, packInstallDirReference,
                    sourceDir, $"CG_PackageContents_{packNumber}");
                wixproj.AddPreprocessorDefinition(sourceDir, pack.DestinationDirectory);
                packNumber++;
            }
#nullable disable
            //  Replace single ComponentGroupRef from Product.wxs with a ref for each pack
            string productWxsPath = EmbeddedTemplates.Extract("Product.wxs", WixSourceDirectory);
            var productDoc = XDocument.Load(productWxsPath);
            var ns2 = productDoc.Root.Name.Namespace;
            var componentGroupRefElement = productDoc.Root.Descendants(ns2 + "ComponentGroupRef").Single();
            componentGroupRefElement.ReplaceWith(Enumerable.Range(1, _package.Packs.Count).Select(n => new XElement(ns2 + "ComponentGroupRef", new XAttribute("Id", "CG_PackageContents_" + n))));
            productDoc.Save(productWxsPath);
#nullable enable

            return wixproj;
        }

        //        public override ITaskItem Build(string outputPath, ITaskItem[] iceSuppressions)
        //        {
        //            List<string> packageContentWxsFiles = new List<string>();

        //            int packNumber = 1;

        //            MsiDirectory dotnetHomeDirectory = new MsiDirectory("dotnet", "DOTNETHOME");
        //            Dictionary<string, string> sourceDirectoryNamesAndValues = new();

        //            foreach (var pack in _package.Packs)
        //            {
        //                string packageContentWxs = Path.Combine(WixSourceDirectory, $"PackageContent.{pack.Id}.wxs");

        //                string directoryReference;
        //                if (pack.Kind == WorkloadPackKind.Library)
        //                {
        //                    directoryReference = dotnetHomeDirectory.GetSubdirectory("library-packs", "LibraryPacksDir").Id;
        //                }
        //                else if (pack.Kind == WorkloadPackKind.Template)
        //                {
        //                    directoryReference = dotnetHomeDirectory.GetSubdirectory("template-packs", "TemplatePacksDir").Id;
        //                }
        //                else
        //                {
        //                    var versionDir = dotnetHomeDirectory.GetSubdirectory("packs", "PacksDir")
        //                        .GetSubdirectory(pack.Id, "PackDir" + packNumber)
        //                        .GetSubdirectory($"{pack.PackageVersion}", "PackVersionDir" + packNumber);

        //                    directoryReference = versionDir.Id;
        //                }

        //                HarvesterToolTask heat = new(BuildEngine, "WixToolsetPath")
        //                {
        //                    DirectoryReference = directoryReference,
        //                    OutputFile = packageContentWxs,
        //                    Platform = this.Platform,
        //                    SourceDirectory = pack.DestinationDirectory,
        //                    SourceVariableName = "SourceDir" + packNumber,
        //                    ComponentGroupName = "CG_PackageContents" + packNumber
        //                };

        //                sourceDirectoryNamesAndValues[heat.SourceVariableName] = heat.SourceDirectory;

        //                if (!heat.Execute())
        //                {
        //                    throw new Exception(Strings.HeatFailedToHarvest);
        //                }

        //                packageContentWxsFiles.Add(packageContentWxs);

        //                packNumber++;
        //            }

        //            //  Create wxs file from dotnetHomeDirectory structure
        //            string directoriesWxsPath = EmbeddedTemplates.Extract("Directories.wxs", WixSourceDirectory);
        //            var directoriesDoc = XDocument.Load(directoriesWxsPath);
        //            var dotnetHomeElement = directoriesDoc.Root.Descendants().Where(d => (string)d.Attribute("Id") == "DOTNETHOME").Single();
        //            //  Remove existing subfolders of DOTNETHOME, which are for single pack MSI
        //            dotnetHomeElement.ReplaceWith(dotnetHomeDirectory.ToXml());
        //            directoriesDoc.Save(directoriesWxsPath);

        //            //  Replace single ComponentGroupRef from Product.wxs with a ref for each pack
        //            string productWxsPath = EmbeddedTemplates.Extract("Product.wxs", WixSourceDirectory);
        //            var productDoc = XDocument.Load(productWxsPath);
        //            var ns = productDoc.Root.Name.Namespace;
        //            var componentGroupRefElement = productDoc.Root.Descendants(ns + "ComponentGroupRef").Single();
        //            componentGroupRefElement.ReplaceWith(Enumerable.Range(1, _package.Packs.Count).Select(n => new XElement(ns + "ComponentGroupRef", new XAttribute("Id", "CG_PackageContents" + n))));
        //            productDoc.Save(productWxsPath);

        //            // Add registry keys for packs in the pack group.
        //            string registryWxsPath = EmbeddedTemplates.Extract("Registry.wxs", WixSourceDirectory);
        //            var registryDoc = XDocument.Load(registryWxsPath);
        //            ns = registryDoc.Root.Name.Namespace;
        //            var registryKeyElement = registryDoc.Root.Descendants(ns + "RegistryKey").Single();
        //            foreach (var pack in _package.Packs)
        //            {
        //                registryKeyElement.Add(new XElement(ns + "RegistryKey", new XAttribute("Key", pack.Id),
        //                                        new XElement(ns + "RegistryKey", new XAttribute("Key", pack.PackageVersion),
        //                                        new XElement(ns + "RegistryValue", new XAttribute("Value", ""), new XAttribute("Type", "string")))));
        //            }
        //            registryDoc.Save(registryWxsPath);

        //            CompilerToolTask candle = CreateDefaultCompiler();

        //            candle.AddSourceFiles(packageContentWxsFiles);

        //            candle.AddSourceFiles(
        //                EmbeddedTemplates.Extract("DependencyProvider.wxs", WixSourceDirectory),
        //                directoriesWxsPath,
        //                EmbeddedTemplates.Extract("dotnethome_x64.wxs", WixSourceDirectory),
        //                productWxsPath,
        //                registryWxsPath);

        //            // Only extract the include file as it's not compilable, but imported by various source files.
        //            EmbeddedTemplates.Extract("Variables.wxi", WixSourceDirectory);

        //            // Workload packs are not upgradable so the upgrade code is generated using the package identity as that
        //            // includes the package version.


        ////            candle.AddPreprocessorDefinition(PreprocessorDefinitionNames.UpgradeCode, $"{upgradeCode:B}");
        ////            candle.AddPreprocessorDefinition(PreprocessorDefinitionNames.DependencyProviderKeyName, $"{providerKeyName}");
        //            candle.AddPreprocessorDefinition(PreprocessorDefinitionNames.InstallationRecordKey, $"InstalledPackGroups");
        //            foreach (var kvp in sourceDirectoryNamesAndValues)
        //            {
        //                candle.AddPreprocessorDefinition(kvp.Key, kvp.Value);
        //            }

        //            if (!candle.Execute())
        //            {
        //                throw new Exception(Strings.FailedToCompileMsi);
        //            }

        //            string msiFileName = Path.Combine(outputPath, OutputName);

        //            ITaskItem msi = Link(candle.OutputPath, msiFileName, iceSuppressions);

        //            AddDefaultPackageFiles(msi);

        //            return msi;
        //        }

        class MsiDirectory
        {
            public string Name { get; }
            public string Id { get; }

            public Dictionary<string, MsiDirectory> Subdirectories { get; } = new();

            public MsiDirectory(string name, string id)
            {
                Name = name;
                Id = id;
            }

            public MsiDirectory GetSubdirectory(string name, string id)
            {
                if (Subdirectories.TryGetValue(name, out var subdir))
                {
                    if (!subdir.Id.Equals(id, StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"ID {id} didn't match existing ID {subdir.Id} for directory {name}.");
                    }
                    return subdir;
                }

                subdir = new MsiDirectory(name, id);
                Subdirectories.Add(name, subdir);
                return subdir;
            }

            public XElement ToXml()
            {
                XNamespace ns = "http://schemas.microsoft.com/wix/2006/wi";
                var xml = new XElement(ns + "Directory");
                xml.SetAttributeValue("Id", Id);
                xml.SetAttributeValue("Name", Name);

                foreach (var subdir in Subdirectories.Values)
                {
                    xml.Add(subdir.ToXml());
                }

                return xml;
            }
        }
    }
}

#nullable disable
