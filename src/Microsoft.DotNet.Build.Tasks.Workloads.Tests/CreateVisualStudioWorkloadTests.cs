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
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Tests
{
    [Collection("Workload Creation")]
    public class CreateVisualStudioWorkloadTests : TestBase
    {
        [SkipOnCI(reason: "This test builds the full WASM workload.")]
        [WindowsOnlyFact]
        public static void ItCreatesPackGroups()
        {
            string packageSource = Path.Combine(TestAssetsPath, "wasm");
            // Create intermediate outputs under %temp% to avoid path issues and make sure it's clean so we don't pick up
            // conflicting sources from previous runs.
            string baseIntermediateOutputPath = Path.Combine(Path.GetTempPath(), "WLPG");

            if (Directory.Exists(baseIntermediateOutputPath))
            {
                Directory.Delete(baseIntermediateOutputPath, recursive: true);
            }

            ITaskItem[] manifestsPackages =
            {
                new TaskItem(Path.Combine(packageSource, "microsoft.net.workload.mono.toolchain.current.manifest-10.0.100.10.0.100.nupkg"))
                .WithMetadata(Metadata.MsiVersion, "10.0.456")
            };

            IBuildEngine buildEngine = new MockBuildEngine();
            CreateVisualStudioWorkload createWorkloadTask = new CreateVisualStudioWorkload()
            {
                AllowMissingPacks = true,
                BaseOutputPath = TestBase.BaseOutputPath,
                BaseIntermediateOutputPath = baseIntermediateOutputPath,
                BuildEngine = buildEngine,
                ComponentResources = Array.Empty<ITaskItem>(),
                CreateWorkloadPackGroups = true,
                DisableParallelPackageGroupProcessing = false,
                IsOutOfSupportInVisualStudio = false,
                ManifestMsiVersion = null,
                PackageSource = packageSource,
                ShortNames = Array.Empty<ITaskItem>(),
                WixToolsetPath = WixToolsetPath,
                WorkloadManifestPackageFiles = manifestsPackages
            };

            bool result = createWorkloadTask.Execute();
            Assert.True(result);

            // Verify that the Visual Studio workload components reference workload pack groups.
            string componentSwr = File.ReadAllText(
                Path.Combine(Path.GetDirectoryName(
                    createWorkloadTask.SwixProjects.FirstOrDefault(
                        i => i.ItemSpec.Contains("wasm.tools.10.0.swixproj")).ItemSpec), "component.swr"));
            Assert.Contains("vs.dependency id=wasm.tools.WorkloadPacks", componentSwr);

            // Manifest installers should contain additional JSON files describing pack groups.
            ITaskItem manifestMsi = createWorkloadTask.Msis.First(m => m.GetMetadata(Metadata.PackageType) == DefaultValues.ManifestMsi);
            MsiUtils.GetAllFiles(manifestMsi.ItemSpec).Should().Contain(f => f.FileName.EndsWith("WorkloadPackGroups.json"));
        }
    }
}
