// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Arcade.Test.Common;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Workloads.Msi;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using WixToolset.Dtf.WindowsInstaller;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Tests
{
    [Collection("MSI tests")]
    public class MsiTests : TestBase
    {
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
            TaskItem packageItem = new(packagePath);
            WorkloadManifestPackage pkg = new(packageItem, Path.Combine(outputDirectory, "pkg"), new Version(msiVersion));
            pkg.Extract();
            WorkloadManifestMsi msi = new(pkg, platform, new MockBuildEngine(), outputDirectory,
                allowSideBySideInstalls, overridePackageVersions: true, generateWixpack: generateWixpack,
                wixpackOutputDirectory: wixpackOutputDirectory);

            return msi.Build(Path.Combine(outputDirectory, "msi"));
        }

        [WindowsOnlyFact]
        public void WorkloadManifestsIncludeInstallationRecords()
        {
            ITaskItem msi603 = BuildManifestMsi(GetTestCaseDirectory(), Path.Combine(TestAssetsPath, "microsoft.net.workload.mono.toolchain.manifest-6.0.200.6.0.3.nupkg"));
            string msiPath603 = msi603.GetMetadata(Metadata.FullPath);

            MsiUtils.GetAllRegistryKeys(msiPath603).Should().Contain(r =>
              r.Key == @"SOFTWARE\Microsoft\dotnet\InstalledManifests\x64\Microsoft.NET.Workload.Mono.ToolChain.Manifest-6.0.200\6.0.3");
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
            WorkloadPackId packId = new("Microsoft.NET.Runtime.Emscripten.Sdk");
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

            // Verify workload record
            MsiUtils.GetAllRegistryKeys(msiPath).Should().Contain(r =>
              r.Key == @"SOFTWARE\Microsoft\dotnet\InstalledPacks\x64\Microsoft.NET.Runtime.Emscripten.2.0.23.Sdk.win-x64\6.0.4");

            // Process the summary information stream's template to extract the MSIs target platform.
            using SummaryInfo si = new(msiPath, enableWrite: false);
            Assert.Equal("x64;1033", si.Template);

            // Verify pack directories
            MsiUtils.GetAllDirectories(msiPath).Select(d => d.Directory).Should().Contain("PackageDir", "because it's an SDK pack");
            MsiUtils.GetAllDirectories(msiPath).Select(d => d.Directory).Should().Contain("InstallDir", "because it's a workload pack");

            // UpgradeCode is predictable/stable for pack MSIs since they are seeded using the package identity (ID & version).
            Assert.Equal("{A06E6854-C6B0-3C8D-8D0C-F0704755303B}", MsiUtils.GetProperty(msiPath, MsiProperty.UpgradeCode));
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
            Assert.Equal("{E4761192-882D-38E9-A3F4-14B6C4AD12BD}", MsiUtils.GetProperty(msiPath, MsiProperty.UpgradeCode));
            Assert.Equal("1.2.3", MsiUtils.GetProperty(msiPath, MsiProperty.ProductVersion));
            Assert.Equal("Microsoft.NET.Workload.Mono.ToolChain,6.0.200,x64", MsiUtils.GetProviderKeyName(msiPath));
            Assert.Equal("x64;1033", si.Template);

            // There should be no version directory present if the old upgrade model is used.
            MsiUtils.GetAllDirectories(msiPath).Select(d => d.Directory).Should().NotContain("ManifestVersionDir", 
                "because the manifest MSI supports major upgrades");

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
            TemplatePackPackage pkg = new(templatePack, packagePath, new[] { "x64" }, pkgDirectory);
            pkg.Extract();
            var buildEngine = new MockBuildEngine();
            WorkloadPackMsi msi = new(pkg, "x64", buildEngine, WixToolsetPath, outputDirectory, overridePackageVersions: true);
            ITaskItem item = msi.Build(msiDirectory);

            string msiPath = item.GetMetadata(Metadata.FullPath);

            // Process the summary information stream's template to extract the MSIs target platform.
            using SummaryInfo si = new(msiPath, enableWrite: false);

            // UpgradeCode is predictable/stable for pack MSIs since they are seeded using the package identity (ID & version).
            Assert.Equal("{EC4D6B34-C9DE-3984-97FD-B7AC96FA536A}", MsiUtils.GetProperty(msiPath, MsiProperty.UpgradeCode));
            // The version is set using the package major.minor.patch
            Assert.Equal("15.2.302.0", MsiUtils.GetProperty(msiPath, MsiProperty.ProductVersion));
            Assert.Equal("Microsoft.iOS.Templates,15.2.302-preview.14.122,x64", MsiUtils.GetProviderKeyName(msiPath));
            Assert.Equal("x64;1033", si.Template);

            // Template packs should pull in the raw nupkg. We can verify by query the File table. There should
            // only be a single file.
            FileRow fileRow = MsiUtils.GetAllFiles(msiPath).FirstOrDefault();
            Assert.Contains("microsoft.ios.templates.15.2.302-preview.14.122.nupkg", fileRow.FileName);

            MsiUtils.GetAllDirectories(msiPath).Select(d => d.Directory).Should().NotContain("PackageDir", "because it's a template pack");
            MsiUtils.GetAllDirectories(msiPath).Select(d => d.Directory).Should().Contain("InstallDir", "because it's a workload pack");
        }
    }
}
