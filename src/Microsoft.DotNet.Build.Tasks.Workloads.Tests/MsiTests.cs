// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Arcade.Test.Common;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Workloads.Msi;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.Win32;
using WixToolset.Dtf.WindowsInstaller;
using Xunit;
using static Microsoft.DotNet.Build.Tasks.Workloads.Msi.WorkloadManifestMsi;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Tests
{
    [Collection("MSI tests")]
    public class MsiTests : TestBase
    {
        private static void ValidateInstallationRecord(IEnumerable<RegistryRow> registryKeys, 
            string installationRecordKeyName, string expectedProviderKey, string expectedProductCode, string expectedUpgradeCode,
            string expectedProductVersion,
            string expectedProductLanguage = "#1033")
        {
            registryKeys.Should().Contain(r => r.Key == installationRecordKeyName &&
                r.Root == 2 &&
                r.Name == "DependencyProviderKey" &&
                r.Value == expectedProviderKey);
            registryKeys.Should().Contain(r => r.Key == installationRecordKeyName &&
                r.Root == 2 &&
                r.Name == "ProductCode" &&
                string.Equals(r.Value, expectedProductCode, StringComparison.OrdinalIgnoreCase));
            registryKeys.Should().Contain(r => r.Key == installationRecordKeyName &&
                r.Root == 2 &&
                r.Name == "UpgradeCode" &&
                string.Equals(r.Value, expectedUpgradeCode, StringComparison.OrdinalIgnoreCase));
            registryKeys.Should().Contain(r => r.Key == installationRecordKeyName &&
                r.Root == 2 &&
                r.Name == "ProductVersion" &&
                r.Value == expectedProductVersion);
            registryKeys.Should().Contain(r => r.Key == installationRecordKeyName &&
                r.Root == 2 &&
                r.Name == "ProductLanguage" &&
                r.Value == expectedProductLanguage);
        }

        private static void ValidateDependencyProviderKey(IEnumerable<RegistryRow> registryKeys, string dependencyProviderKeyName)
        {
            // Dependency provider entries references the ProductVersion and ProductName properties. These
            // properties are set by the installer service at install time.
            registryKeys.Should().Contain(r => r.Key == dependencyProviderKeyName &&
                    r.Root == -1 &&
                    r.Name == "Version" &&
                    r.Value == "[ProductVersion]");
            registryKeys.Should().Contain(r => r.Key == dependencyProviderKeyName &&
                r.Root == -1 &&
                r.Name == "DisplayName" &&
                r.Value == "[ProductName]");
        }

        /// <summary>
        /// Helper method for generating workload manifest MSIs.
        /// </summary>
        /// <param name="outputDirectory">The directory to use for generated output (WiX project, MSI, etc.)</param>
        /// <param name="packagePath">The path of the workload manifest NuGet package.</param>
        /// <param name="msiVersion">The ProductVersion to assign to the MSI.</param>
        /// <param name="platform">The platform of the MSI.</param>
        /// <param name="allowSideBySideInstalls">Whether MSIs should allow side-by-side installations instead of major upgrades.</param>
        /// <returns>A task item with metadata for the generated MSI.</returns>
        private static ITaskItem BuildManifestMsi(string outputDirectory, string packagePath, string msiVersion = "1.2.3", string platform = "x64",
            bool allowSideBySideInstalls = true, bool generateWixpack = false, string wixpackOutputDirectory = null)
        {
            Directory.CreateDirectory(outputDirectory);
            File.Copy(Path.Combine(TestAssetsPath, "NuGet.config"), Path.Combine(outputDirectory, "NuGet.config"), overwrite: true);
            TaskItem packageItem = new(packagePath);
            WorkloadManifestPackage pkg = new(packageItem, Path.Combine(outputDirectory, "pkg"), new Version(msiVersion));
            pkg.Extract();
            WorkloadManifestMsi msi = new(pkg, platform, new MockBuildEngine(), outputDirectory,
                allowSideBySideInstalls, overridePackageVersions: true, generateWixpack: generateWixpack,
                wixpackOutputDirectory: wixpackOutputDirectory);

            return msi.Build(Path.Combine(outputDirectory, "msi"));
        }

        [WindowsOnlyFact]
        public void ItCanBuildWorkloadSdkPackMsi()
        {
            string testCaseDirectory = GetTestCaseDirectory();
            string packageContentsDirectory = Path.Combine(testCaseDirectory, "pkg");
            string msiOutputDirectory = Path.Combine(testCaseDirectory, "msi");

            TaskItem packageItem = new(Path.Combine(TestAssetsPath, "microsoft.net.workload.emscripten.manifest-6.0.200.6.0.4.nupkg"));
            WorkloadManifestPackage manifestPackage = new(packageItem, packageContentsDirectory, new Version("1.2.3"));
            // Parse the manifest to extract information related to workload packs so we can extract a specific pack.
            WorkloadManifest manifest = manifestPackage.GetManifest();
            //WorkloadPackId packId = new("Microsoft.NET.Runtime.Emscripten.Sdk");
            WorkloadPackId packId = new("Microsoft.NET.Runtime.Emscripten.Python");
            // Microsoft.NET.Runtime.Emscripten.Python
            WorkloadPack pack = manifest.Packs[packId];

            var sourcePackages = WorkloadPackPackage.GetSourcePackages(TestAssetsPath, pack);
            var sourcePackageInfo = sourcePackages.FirstOrDefault();
            var workloadPackPackage = WorkloadPackPackage.Create(pack, sourcePackageInfo.sourcePackage, sourcePackageInfo.platforms, packageContentsDirectory, null, null);
            workloadPackPackage.Extract();
            var workloadPackMsi = new WorkloadPackMsi(workloadPackPackage, "x64", new MockBuildEngine(),
                WixToolsetPath, testCaseDirectory, overridePackageVersions: true);

            // Build the MSI and verify its contents
            var msi = workloadPackMsi.Build(msiOutputDirectory);
            string msiPath = msi.GetMetadata(Metadata.FullPath);

            // Process the summary information stream's template to extract the MSIs target platform.
            using SummaryInfo si = new(msiPath, enableWrite: false);
            Assert.Equal("x64;1033", si.Template);

            // Verify pack directories
            MsiUtils.GetAllDirectories(msiPath).Select(d => d.Directory).Should().Contain("PackageDir", "because it's an SDK pack");
            MsiUtils.GetAllDirectories(msiPath).Select(d => d.Directory).Should().Contain("InstallDir", "because it's a workload pack");

            // UpgradeCode is predictable/stable for pack MSIs since they are seeded using the package identity (ID & version).
            string upgradeCode = MsiUtils.GetProperty(msiPath, MsiProperty.UpgradeCode);
            Assert.Equal("{BDE8712D-9BD7-3692-9C2A-C518208967D6}", upgradeCode);

            // Verify the installation record and dependency provider registry entries
            var registryKeys = MsiUtils.GetAllRegistryKeys(msiPath);
            string expectedProductCode = MsiUtils.GetProperty(msiPath, MsiProperty.ProductCode);
            string installationRecordKeyName = @"SOFTWARE\Microsoft\dotnet\InstalledPacks\x64\Microsoft.NET.Runtime.Emscripten.2.0.23.Python.win-x64\6.0.4";
            string dependencyProviderKeyName = @"Software\Classes\Installer\Dependencies\Microsoft.NET.Runtime.Emscripten.2.0.23.Python.win-x64,6.0.4,x64";

            ValidateInstallationRecord(registryKeys, installationRecordKeyName,
                "Microsoft.NET.Runtime.Emscripten.2.0.23.Python.win-x64,6.0.4,x64",
                expectedProductCode, upgradeCode, "6.0.4.0");
            ValidateDependencyProviderKey(registryKeys, dependencyProviderKeyName);
        }

        [WindowsOnlyFact]
        public void ItCanBuildSideBySideManifestMsis()
        {
            string outputDirectory = GetTestCaseDirectory();

            // Build 6.0.200 manifest for version 6.0.3
            ITaskItem msi603 = BuildManifestMsi(outputDirectory, Path.Combine(TestAssetsPath, "microsoft.net.workload.mono.toolchain.manifest-6.0.200.6.0.3.nupkg"));
            string msiPath603 = msi603.GetMetadata(Metadata.FullPath);

            // Build 6.0.200 manifest for version 6.0.4
            ITaskItem msi604 = BuildManifestMsi(outputDirectory, Path.Combine(TestAssetsPath, "microsoft.net.workload.mono.toolchain.manifest-6.0.200.6.0.4.nupkg"));
            string msiPath604 = msi604.GetMetadata(Metadata.FullPath);

            // For upgradable MSIs, the 6.0.4 and 6.0.3 copies of the package would have generated the same
            // upgrade code to ensure upgrades along the manifest feature band. For SxS they should be different.
            Assert.NotEqual(MsiUtils.GetProperty(msiPath603, MsiProperty.UpgradeCode), MsiUtils.GetProperty(msiPath604, MsiProperty.UpgradeCode));

            // Provider keys for SxS MSIs should be different while upgrade related MSIs should have stable provider keys.
            Assert.Equal("Microsoft.NET.Workload.Mono.ToolChain,6.0.200,6.0.3,x64", MsiUtils.GetProviderKeyName(msiPath603));
            Assert.Equal("Microsoft.NET.Workload.Mono.ToolChain,6.0.200,6.0.4,x64", MsiUtils.GetProviderKeyName(msiPath604));

            // WiX populates the DefaultDir column using "short name | long name" pairs.
            MsiUtils.GetAllDirectories(msiPath603).Should().Contain(d =>
                d.Directory == "ManifestVersionDir" &&
                d.DirectoryParent == "ManifestIdDir" &&
                d.DefaultDir.EndsWith("|6.0.3"));
            MsiUtils.GetAllDirectories(msiPath604).Should().Contain(d =>
                d.Directory == "ManifestVersionDir" &&
                d.DirectoryParent == "ManifestIdDir" &&
                d.DefaultDir.EndsWith("|6.0.4"));
        }

        [WindowsOnlyFact]
        public void ItCanBuildAManifestMsi()
        {
            string outputDirectory = GetTestCaseDirectory();
            string wixpackOutputDirectory = Path.Combine(outputDirectory, "wixpack");

            ITaskItem msi = BuildManifestMsi(outputDirectory,
                Path.Combine(TestAssetsPath, "microsoft.net.workload.mono.toolchain.manifest-6.0.200.6.0.3.nupkg"),
                allowSideBySideInstalls: false,
                generateWixpack: true,
                wixpackOutputDirectory: wixpackOutputDirectory);

            string msiPath = msi.GetMetadata(Metadata.FullPath);

            // Process the summary information stream's template to extract the MSIs target platform.
            using SummaryInfo si = new(msiPath, enableWrite: false);

            // UpgradeCode is predictable/stable for manifest MSIs that support major upgrades.
            string upgradeCode = MsiUtils.GetProperty(msiPath, MsiProperty.UpgradeCode);
            Assert.Equal("{E4761192-882D-38E9-A3F4-14B6C4AD12BD}", upgradeCode);
            Assert.Equal("1.2.3", MsiUtils.GetProperty(msiPath, MsiProperty.ProductVersion));
            Assert.Equal("Microsoft.NET.Workload.Mono.ToolChain,6.0.200,x64", MsiUtils.GetProviderKeyName(msiPath));
            Assert.Equal("x64;1033", si.Template);

            // There should be no version directory present if the old upgrade model is used.
            MsiUtils.GetAllDirectories(msiPath).Select(d => d.Directory).Should().NotContain("ManifestVersionDir",
                "because the manifest MSI supports major upgrades");

            // Verify the installation record and dependency provider registry entries
            var registryKeys = MsiUtils.GetAllRegistryKeys(msiPath);
            string expectedProductCode = MsiUtils.GetProperty(msiPath, MsiProperty.ProductCode);
            string installationRecordKeyName = @"SOFTWARE\Microsoft\dotnet\InstalledManifests\x64\Microsoft.NET.Workload.Mono.ToolChain.Manifest-6.0.200\6.0.3";
            string dependencyProviderKeyName = @"Software\Classes\Installer\Dependencies\Microsoft.NET.Workload.Mono.ToolChain,6.0.200,x64";

            ValidateInstallationRecord(registryKeys, installationRecordKeyName,
                "Microsoft.NET.Workload.Mono.ToolChain,6.0.200,x64",
                expectedProductCode, upgradeCode, "1.2.3");
            ValidateDependencyProviderKey(registryKeys, dependencyProviderKeyName);

            // The File table should contain the workload manifest and targets. There may be additional
            // localized content for the manifests. Their presence is neither required nor critical to
            // how workloads functions.
            var files = MsiUtils.GetAllFiles(msiPath);
            files.Should().Contain(f => f.FileName.EndsWith("WorkloadManifest.json"));
            files.Should().Contain(f => f.FileName.EndsWith("WorkloadManifest.targets"));

            // Verify that the wixpack archive was created.
            Assert.True(File.Exists(msi.GetMetadata(Metadata.Wixpack)));
        }        

        [WindowsOnlyFact]
        public void ItCanBuildATemplatePackMsi()
        {
            string packagePath = Path.Combine(TestAssetsPath, "microsoft.ios.templates.15.2.302-preview.14.122.nupkg");
            string outputDirectory = GetTestCaseDirectory();
            string pkgDirectory = Path.Combine(outputDirectory, "pkg");
            string msiDirectory = Path.Combine(outputDirectory, "msi");
            WorkloadPack templatePack = new(new WorkloadPackId("Microsoft.iOS.Templates"), "15.2.302-preview.14.122", WorkloadPackKind.Template, null);
            TemplatePackPackage pkg = new(templatePack, packagePath, ["x64"], pkgDirectory);
            pkg.Extract();
            var buildEngine = new MockBuildEngine();
            WorkloadPackMsi msi = new(pkg, "x64", buildEngine, WixToolsetPath, outputDirectory, overridePackageVersions: true);
            ITaskItem item = msi.Build(msiDirectory);

            string msiPath = item.GetMetadata(Metadata.FullPath);

            // Process the summary information stream's template to extract the MSIs target platform.
            using SummaryInfo si = new(msiPath, enableWrite: false);

            // UpgradeCode is predictable/stable for pack MSIs since they are seeded using the package identity (ID & version).
            string upgradeCode = MsiUtils.GetProperty(msiPath, MsiProperty.UpgradeCode);
            Assert.Equal("{EC4D6B34-C9DE-3984-97FD-B7AC96FA536A}", upgradeCode);
            // The version is set using the package major.minor.patch
            Assert.Equal("15.2.302.0", MsiUtils.GetProperty(msiPath, MsiProperty.ProductVersion));
            Assert.Equal("Microsoft.iOS.Templates,15.2.302-preview.14.122,x64", MsiUtils.GetProviderKeyName(msiPath));
            Assert.Equal("x64;1033", si.Template);

            // Template packs should pull in the raw nupkg. We can verify by query the File table. There should
            // only be a single file.
            FileRow fileRow = MsiUtils.GetAllFiles(msiPath).FirstOrDefault();
            Assert.Contains("microsoft.ios.templates.15.2.302-preview.14.122.nupkg", fileRow.FileName);

            var directories = MsiUtils.GetAllDirectories(msiPath).Select(d => d.Directory);
            directories.Should().NotContain("PackageDir", "because it's a template pack");
            directories.Should().Contain("InstallDir", "because it's a workload pack");

            // Verify the installation record and dependency provider registry entries
            var registryKeys = MsiUtils.GetAllRegistryKeys(msiPath);
            string expectedProductCode = MsiUtils.GetProperty(msiPath, MsiProperty.ProductCode);
            string installationRecordKeyName = @"SOFTWARE\Microsoft\dotnet\InstalledPacks\x64\Microsoft.iOS.Templates\15.2.302-preview.14.122";
            string dependencyProviderKeyName = @"Software\Classes\Installer\Dependencies\Microsoft.iOS.Templates,15.2.302-preview.14.122,x64";

            ValidateInstallationRecord(registryKeys, installationRecordKeyName,
                "Microsoft.iOS.Templates,15.2.302-preview.14.122,x64", expectedProductCode, upgradeCode, "15.2.302.0");
            ValidateDependencyProviderKey(registryKeys, dependencyProviderKeyName);
        }

        [WindowsOnlyFact]
        public void ItCanBuildAWorkPackGroupMsi()
        {
            string outputDirectory = GetTestCaseDirectory();
            string packageContentsDirectory = Path.Combine(outputDirectory, "pkg");
            string msiOutputDirectory = Path.Combine(outputDirectory, "msi");
            string pkgOutputDirectory = Path.Combine(outputDirectory, "nuget");
            string packageSource = Path.Combine(TestAssetsPath, "wasm");

            TaskItem packageItem = new(Path.Combine(packageSource, "microsoft.net.workload.mono.toolchain.current.manifest-10.0.100.10.0.100.nupkg"));
            WorkloadManifestPackage manifestPackage = new(packageItem, packageContentsDirectory, new Version("1.2.3"));
            // Parse the manifest to extract information related to workload packs so we can extract a specific pack.
            WorkloadManifest manifest = manifestPackage.GetManifest();
            WorkloadId workloadId = new("wasm-tools");
            WorkloadDefinition workload = (WorkloadDefinition)manifest.Workloads[workloadId];

            string packGroupId = null;
            WorkloadPackGroupJson packGroupJson = null;

            packGroupId = WorkloadPackGroupPackage.GetPackGroupID(workload.Id);
            packGroupJson = new WorkloadPackGroupJson()
            {
                GroupPackageId = packGroupId,
                GroupPackageVersion = manifestPackage.PackageVersion.ToString()
            };

            List<WorkloadPackPackage> workloadPackPackages = [];

            foreach (WorkloadPackId packId in workload.Packs)
            {
                WorkloadPack pack = manifest.Packs[packId];

                packGroupJson.Packs.Add(new WorkloadPackJson()
                {
                    PackId = pack.Id,
                    PackVersion = pack.Version
                });

                string sourcePackage = WorkloadPackPackage.GetSourcePackage(packageSource, pack, "x64");

                if (!string.IsNullOrWhiteSpace(sourcePackage))
                {
                    workloadPackPackages.Add(WorkloadPackPackage.Create(pack, sourcePackage, ["x64"],
                        packageContentsDirectory, null, null));
                }
            }

            var groupPackage = new WorkloadPackGroupPackage(workload.Id);
            groupPackage.Packs.AddRange(workloadPackPackages);
            groupPackage.ManifestsPerPlatform["x64"] = new([manifestPackage]);

            var buildEngine = new MockBuildEngine();

            foreach (var p in workloadPackPackages)
            {
                p.Extract();
            }

            WorkloadPackGroupMsi msi = new(groupPackage, "x64", buildEngine, outputDirectory, overridePackageVersions: true);
            ITaskItem msiWorkloadPackGroupOutputItem = msi.Build(msiOutputDirectory);
            string msiPath = msiWorkloadPackGroupOutputItem.GetMetadata(Metadata.FullPath);

            MsiPayloadPackageProject csproj = new(msi.Metadata, msiWorkloadPackGroupOutputItem, outputDirectory, pkgOutputDirectory, msi.NuGetPackageFiles);
            msiWorkloadPackGroupOutputItem.SetMetadata(Metadata.PackageProject, csproj.Create());

            // Build individual pack MSIs to compare against the pack group.
            var sdkPackPackage = workloadPackPackages.FirstOrDefault(p => p.Id == "Microsoft.NET.Runtime.WebAssembly.Sdk");
            WorkloadPackMsi sdkPackMsi = new(sdkPackPackage, "x64", buildEngine, WixToolsetPath, outputDirectory, overridePackageVersions: true);
            ITaskItem sdkPackMsiItem = sdkPackMsi.Build(msiOutputDirectory);
            string sdkPackMsiPath = sdkPackMsiItem.GetMetadata(Metadata.FullPath);

            // Verify workdload record keys for the pack group.
            MsiUtils.GetAllRegistryKeys(msiPath).Should().Contain(r =>
              r.Key == @"SOFTWARE\Microsoft\dotnet\InstalledPackGroups\x64\wasm.tools.WorkloadPacks\10.0.100\Microsoft.NET.Runtime.WebAssembly.Sdk\10.0.0");
            MsiUtils.GetAllRegistryKeys(msiPath).Should().Contain(r =>
              r.Key == @"SOFTWARE\Microsoft\dotnet\InstalledPackGroups\x64\wasm.tools.WorkloadPacks\10.0.100\Microsoft.NET.Sdk.WebAssembly.Pack\10.0.0");
            MsiUtils.GetAllRegistryKeys(msiPath).Should().Contain(r =>
              r.Key == @"SOFTWARE\Microsoft\dotnet\InstalledPackGroups\x64\wasm.tools.WorkloadPacks\10.0.100\Microsoft.NETCore.App.Runtime.Mono.browser-wasm\10.0.0");
            MsiUtils.GetAllRegistryKeys(msiPath).Should().Contain(r =>
              r.Key == @"SOFTWARE\Microsoft\dotnet\InstalledPackGroups\x64\wasm.tools.WorkloadPacks\10.0.100\Microsoft.NETCore.App.Runtime.AOT.win-x64.Cross.browser-wasm\10.0.0");

            // Verify pack directories
            MsiUtils.GetAllDirectories(msiPath).Select(d => d.Directory).Should().Contain("PacksDir", "because the pack group contains SDK packs");
            MsiUtils.GetAllDirectories(msiPath).Select(d => d.Directory).Should().Contain("LibraryPacksDir", "because the pack group contains a library pack");

            // Individual pack MSIs and pack group should have stable IDs for their components.
            // Pick a unique file from the File table, then locate the matching component in the pack
            // MSI and verify that the pack group MSI contains a component with the same ID.
            FileRow f1 = MsiUtils.GetAllFiles(sdkPackMsiPath).First(f => f.FileName.EndsWith("Sdk.props"));
            ComponentRow c1 = MsiUtils.GetAllComponents(sdkPackMsiPath).First(c => c.Component == f1.Component_);
            MsiUtils.GetAllComponents(msiPath).Should().Contain(c => c.ComponentId == c1.ComponentId,
                "Packs and PackGroups should share components");
        }
    }
}
