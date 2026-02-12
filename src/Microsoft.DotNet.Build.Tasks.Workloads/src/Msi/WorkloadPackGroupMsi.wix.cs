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

        protected override string? MsiPackageType => DefaultValues.WorkloadPackGroupMsi;

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
                    registryKeyElement.Add(new XElement(ns + "RegistryKey", 
                        new XAttribute("Key", $@"{pack.Id}\{pack.PackageVersion}"),
                        new XElement(ns + "RegistryValue", new XAttribute("Value", ""), new XAttribute("Type", "string"))));
                }
                registryDoc.Save(registryWxsPath);
            }
#nullable enable

            int packNumber = 1;

            HashSet<string> directoryReferences = new();

            foreach (var pack in _package.Packs)
            {
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
    }
}

#nullable disable
