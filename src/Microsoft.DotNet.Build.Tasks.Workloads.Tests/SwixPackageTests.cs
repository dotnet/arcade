// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Arcade.Test.Common;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Workloads.Msi;
using Microsoft.DotNet.Build.Tasks.Workloads.Swix;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Tests
{
    public class SwixPackageTests : TestBase
    {
        [WindowsOnlyFact]
        public void ItThrowsIfPackageRelativePathExceedsLimit()
        {
            TaskItem msiItem = new TaskItem("Microsoft.NET.Workload.Mono.ToolChain.Manifest-6.0.100.6.0.0-preview.7.21377.12-x64.msi");
            msiItem.SetMetadata(Metadata.SwixPackageId, "Microsoft.NET.Workload.Mono.ToolChain.Manifest-6.0.100");
            msiItem.SetMetadata(Metadata.Version, "6.0.0.0");
            msiItem.SetMetadata(Metadata.Platform, "x64");

            Exception e = Assert.Throws<Exception>(() =>
            {
                MsiSwixProject swixProject = new(msiItem, BaseIntermediateOutputPath, BaseOutputPath);
            });

            Assert.Equal(@"Relative package path exceeds the maximum length (182): Microsoft.NET.Workload.Mono.ToolChain.Manifest-6.0.100,version=6.0.0.0,chip=x64,productarch=neutral\Microsoft.NET.Workload.Mono.ToolChain.Manifest-6.0.100.6.0.0-preview.7.21377.12-x64.msi.", e.Message);
        }

        [WindowsOnlyFact]
        public void SwixPackageIdsIncludeThePackageVersion()
        {
            // Build to a different path to avoid any file read locks on the MSI from other tests
            // that can open it.
            string PackageRootDirectory = Path.Combine(BaseIntermediateOutputPath, Path.GetRandomFileName());
            string packagePath = Path.Combine(TestAssetsPath, "microsoft.ios.templates.15.2.302-preview.14.122.nupkg");

            WorkloadPack p = new(new WorkloadPackId("Microsoft.iOS.Templates"), "15.2.302-preview.14.122", WorkloadPackKind.Template, null);
            TemplatePackPackage pkg = new(p, packagePath, new[] { "x64" }, PackageRootDirectory);
            pkg.Extract();
            WorkloadPackMsi msi = new(pkg, "x64", new MockBuildEngine(), WixToolsetPath, BaseIntermediateOutputPath);

            ITaskItem item = msi.Build(MsiOutputPath);

            Assert.Equal("Microsoft.iOS.Templates.15.2.302-preview.14.122", item.GetMetadata(Metadata.SwixPackageId));
        }
    }
}
