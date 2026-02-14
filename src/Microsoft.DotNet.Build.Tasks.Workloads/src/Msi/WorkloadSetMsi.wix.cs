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
    internal class WorkloadSetMsi : MsiBase
    {
        private WorkloadSetPackage _package;

        public override string ProductTemplate => "WorkloadSetProduct.wxs";

        protected override string BaseOutputName => Path.GetFileNameWithoutExtension(_package.PackagePath);

        protected override string ProviderKeyName => 
            $"Microsoft.NET.Workload.Set,{_package.SdkFeatureBand},{_package.PackageVersion},{Platform}";

        protected override string? InstallationRecordKey => "InstalledWorkloadSets";

        protected override Guid UpgradeCode => 
            Utils.CreateUuid(UpgradeCodeNamespaceUuid, $"{_package.Identity};{Platform}");

        protected override string? MsiPackageType => DefaultValues.WorkloadSetMsi;

        public WorkloadSetMsi(WorkloadSetPackage package, string platform, IBuildEngine buildEngine,
            string baseIntermediatOutputPath, string wixToolsetVersion = ToolsetInfo.MicrosoftWixToolsetVersion,
            bool overridePackageVersions = false, bool generateWixPack = false) :
            base(MsiMetadata.Create(package), buildEngine, platform, baseIntermediatOutputPath,
                wixToolsetVersion, overridePackageVersions, generateWixPack)
        {
            _package = package;
        }

        protected override WixProject CreateProject()
        {
            WixProject wixproj = base.CreateProject();

            EmbeddedTemplates.Extract("DependencyProvider.wxs", WixSourceDirectory);
            EmbeddedTemplates.Extract("Directories.wxs", WixSourceDirectory);
            EmbeddedTemplates.Extract("dotnethome_x64.wxs", WixSourceDirectory);
            EmbeddedTemplates.Extract("WorkloadSetProduct.wxs", WixSourceDirectory);

            string packageDataDirectory = Path.Combine(_package.DestinationDirectory, "data");
            wixproj.AddHarvestDirectory(packageDataDirectory, MsiDirectories.WorkloadSetVersionDirectory,
                PreprocessorDefinitionNames.SourceDir);

            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.SourceDir, $"{packageDataDirectory}");
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.SdkFeatureBandVersion, $"{_package.SdkFeatureBand}");
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.UpgradeStrategy, "none");
            wixproj.AddPreprocessorDefinition(PreprocessorDefinitionNames.WorkloadSetVersion, $"{_package.WorkloadSetVersion}");

            return wixproj;
        }
    }
}

#nullable disable
