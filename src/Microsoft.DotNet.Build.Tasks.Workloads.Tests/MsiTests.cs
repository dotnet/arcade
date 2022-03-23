// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Arcade.Test.Common;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Deployment.WindowsInstaller;
using Microsoft.DotNet.Build.Tasks.Workloads.Msi;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Tests
{
    [Collection("6.0.200 Toolchain manifest tests")]
    public class MsiTests : TestBase
    {
        [WindowsOnlyFact]        
        public void ItCanBuildAManifestMsi()
        {
            string PackageRootDirectory = Path.Combine(BaseIntermediateOutputPath, "pkg");
            TaskItem manifestPackageItem = new(Path.Combine(TestAssetsPath, "microsoft.net.workload.mono.toolchain.manifest-6.0.200.6.0.3.nupkg"));
            WorkloadManifestPackage pkg = new(manifestPackageItem, PackageRootDirectory, new Version("1.2.3"));
            pkg.Extract();
            WorkloadManifestMsi msi = new(pkg, "x64", new MockBuildEngine(), WixToolsetPath, BaseIntermediateOutputPath);

            ITaskItem item = msi.Build(MsiOutputPath);

            string msiPath = item.GetMetadata(Metadata.FullPath);

            // Process the summary information stream's template to extract the MSIs target platform.
            using SummaryInfo si = new(msiPath, enableWrite: false);

            // UpgradeCode is predictable/stable for manifest MSIs.
            Assert.Equal("{E4761192-882D-38E9-A3F4-14B6C4AD12BD}", MsiUtils.GetProperty(msiPath, MsiProperty.UpgradeCode));
            Assert.Equal("1.2.3", MsiUtils.GetProperty(msiPath, MsiProperty.ProductVersion));
            Assert.Equal("Microsoft.NET.Workload.Mono.ToolChain,6.0.200,x64", MsiUtils.GetProviderKeyName(msiPath));
            Assert.Equal("x64;1033", si.Template);

            // Generated MSI should return the path where the .wixobj files are located so
            // WiX packs can be created for post-build signing.
            Assert.NotNull(item.GetMetadata(Metadata.WixObj));
        }       
    }
}
