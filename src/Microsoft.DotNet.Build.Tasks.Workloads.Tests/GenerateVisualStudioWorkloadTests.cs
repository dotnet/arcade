// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Arcade.Test.Common;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Tests
{
    public class GenerateVisualStudioWorkloadTests
    {
        public string IntermediateBaseOutputPath = Path.Combine(AppContext.BaseDirectory, "obj");

        public static readonly string TestAssetsPath = Path.Combine(AppContext.BaseDirectory, "testassets");

        public string TestIntermediateBaseOutputPath => Path.Combine(IntermediateBaseOutputPath, Path.GetFileNameWithoutExtension(Path.GetTempFileName()));

        [Fact]
        public void ItIgnoresNotApplicableAliasedPacks()
        {
            string workloadManifest = Path.Combine(AppContext.BaseDirectory, "testassets", "AbstractWorkloadsNonWindowsPacks.json");

            var buildTask = new GenerateVisualStudioWorkload()
            {
                WorkloadManifests = new TaskItem[]
                {
                    new TaskItem(workloadManifest)
                },
                ComponentVersions = new TaskItem[]
                {
                    new TaskItem("microsoft-net-runtime-ios", new Dictionary<string, string> { { "Version", "6.5.38766" } }),
                    new TaskItem("runtimes-ios", new Dictionary<string, string> { { "Version", "6.5.38766" } }),
                    new TaskItem("microsoft-net-runtime-mono-tooling", new Dictionary<string, string> { { "Version", "6.5.38766" } }),
                },
                GenerateMsis = false,
                IntermediateBaseOutputPath = TestIntermediateBaseOutputPath,
                WixToolsetPath = "",
                BuildEngine = new MockBuildEngine()
            };

            Assert.True(buildTask.Execute());
            string outputPath = Path.GetDirectoryName(buildTask.SwixProjects[0].GetMetadata("FullPath"));
            string componentSwr = File.ReadAllText(Path.Combine(outputPath, "component.swr"));

            Assert.Contains(@"package name=microsoft.net.runtime.ios", componentSwr);
            Assert.DoesNotContain(@"vs.dependency id=Microsoft.NETCore.App.Runtime.AOT.Cross.ios-arm", componentSwr);
            Assert.DoesNotContain(@"vs dependency id=Microsoft.NETCore.App.Runtime.AOT.Cross.ios-arm64", componentSwr);
            Assert.DoesNotContain(@"vs dependency id=Microsoft.NETCore.App.Runtime.AOT.Cross.iossimulator-arm64", componentSwr);
            Assert.DoesNotContain(@"vs dependency id=Microsoft.NETCore.App.Runtime.AOT.Cross.iossimulator-x64", componentSwr);
            Assert.DoesNotContain(@"vs dependency id=Microsoft.NETCore.App.Runtime.AOT.Cross.iossimulator-x86", componentSwr);
            Assert.Contains(@"vs.dependency id=runtimes.ios", componentSwr);
        }

        [Fact]
        public void ItGeneratesASwixProjectFromAWorkloadManifest()
        {
            string workloadManifest = Path.Combine(AppContext.BaseDirectory, "testassets", "WorkloadManifest.json");

            var buildTask = new GenerateVisualStudioWorkload()
            {
                WorkloadManifests = new TaskItem[]
                {
                    new TaskItem(workloadManifest)
                },
                ComponentVersions = new TaskItem[]
                {
                    new TaskItem("microsoft-net-sdk-blazorwebassembly-aot", new Dictionary<string, string> { { "Version", "6.5.38766" } }),
                },
                GenerateMsis = false,
                IntermediateBaseOutputPath = TestIntermediateBaseOutputPath,
                WixToolsetPath = "",
                BuildEngine = new MockBuildEngine()
            };

            Assert.True(buildTask.Execute());
            string outputPath = Path.GetDirectoryName(buildTask.SwixProjects[0].GetMetadata("FullPath"));
            string componentSwr = File.ReadAllText(Path.Combine(outputPath, "component.swr"));

            Assert.Single(buildTask.SwixProjects);
            Assert.Contains(@"package name=microsoft.net.sdk.blazorwebassembly.aot
        version=6.5.38766", componentSwr);
            Assert.Contains("vs.dependency id=Microsoft.NET.Runtime.MonoAOTCompiler.Task.6.0.0-preview.4.21201.1", componentSwr);
        }

        [Fact]
        public void ItCanShortenPackageIds()
        {
            string workloadManifest = Path.Combine(AppContext.BaseDirectory, "testassets", "WorkloadManifest.json");

            var buildTask = new GenerateVisualStudioWorkload()
            {
                WorkloadManifests = new TaskItem[]
                {
                    new TaskItem(workloadManifest)
                },
                ShortNames = new TaskItem[]
                {
                    new TaskItem("Microsoft.NET.Runtime", new Dictionary<string, string> { {"Replacement", "MSFT"} })
                },
                ComponentVersions = new TaskItem[]
                {
                    new TaskItem("microsoft-net-sdk-blazorwebassembly-aot", new Dictionary<string, string> { { "Version", "6.5.38766" } }),
                },
                GenerateMsis = false,
                IntermediateBaseOutputPath = TestIntermediateBaseOutputPath,
                WixToolsetPath = "",
                BuildEngine = new MockBuildEngine()
            };

            Assert.True(buildTask.Execute());
            string outputPath = Path.GetDirectoryName(buildTask.SwixProjects[0].GetMetadata("FullPath"));
            string componentSwr = File.ReadAllText(Path.Combine(outputPath, "component.swr"));

            Assert.Single(buildTask.SwixProjects);
            Assert.Contains(@"package name=microsoft.net.sdk.blazorwebassembly.aot
        version=6.5.38766", componentSwr);
            Assert.Contains("vs.dependency id=MSFT.MonoAOTCompiler.Task.6.0.0-preview.4.21201.1", componentSwr);
        }

        [Fact]
        public void ItGeneratesASwixProjectFromAWorkloadManifestPackage()
        {
            string workloadPackage = Path.Combine(AppContext.BaseDirectory, "testassets",
                "microsoft.net.sdk.blazorwebassembly.aot.6.0.0-preview.4.21209.5.nupkg");

            var buildTask = new GenerateVisualStudioWorkload()
            {
                WorkloadPackages = new TaskItem[]
                {
                    new TaskItem(workloadPackage)
                },

                GenerateMsis = false,
                IntermediateBaseOutputPath = TestIntermediateBaseOutputPath,
                WixToolsetPath = "",
                BuildEngine = new MockBuildEngine()
            };

            Assert.True(buildTask.Execute());
            string outputPath = Path.GetDirectoryName(buildTask.SwixProjects[0].GetMetadata("FullPath"));
            string componentSwr = File.ReadAllText(Path.Combine(outputPath, "component.swr"));

            Assert.Single(buildTask.SwixProjects);
            Assert.Contains(@"package name=microsoft.net.sdk.blazorwebassembly.aot
        version=1.0", componentSwr);
            Assert.Contains("vs.dependency id=Microsoft.NET.Runtime.MonoAOTCompiler.Task.6.0.0-preview.4.21201.1", componentSwr);
            Assert.Contains("vs.dependency id=Microsoft.NET.Runtime.Emscripten.Python.6.0.0-preview.4.21205.1", componentSwr);
        }

        [Fact]
        public void ItIncludesAbstractManifests()
        {
            var buildTask = new GenerateVisualStudioWorkload()
            {
                WorkloadManifests = new TaskItem[]
                {
                    new TaskItem(Path.Combine(TestAssetsPath, "BlazorWorkloadManifest.json"))
                },
                GenerateMsis = false,
                IntermediateBaseOutputPath = TestIntermediateBaseOutputPath,
                WixToolsetPath = "",
                BuildEngine = new MockBuildEngine()
            };

            Assert.True(buildTask.Execute());
            string blazorOutputPath = Path.GetDirectoryName(buildTask.SwixProjects[0].GetMetadata("FullPath"));
            string blazorComponentSwr = File.ReadAllText(Path.Combine(blazorOutputPath, "component.swr"));
            Assert.Contains(@"package name=microsoft.net.sdk.blazorwebassembly.aot
        version=6.0.0.0", blazorComponentSwr);
            string androidOutputPath = Path.GetDirectoryName(buildTask.SwixProjects[1].GetMetadata("FullPath"));
            string androidComponentSwr = File.ReadAllText(Path.Combine(androidOutputPath, "component.swr"));
            Assert.Contains(@"package name=microsoft.net.runtime.android
        version=6.0.0.0", androidComponentSwr);
        }

        [Fact]
        public void ItReportsMissingPacks()
        {
            var buildTask = new GenerateVisualStudioWorkload()
            {
                WorkloadManifests = new TaskItem[]
                {
                    new TaskItem(Path.Combine(TestAssetsPath, "BlazorWorkloadManifest.json"))
                },
                GenerateMsis = true,
                IntermediateBaseOutputPath = TestIntermediateBaseOutputPath,
                WixToolsetPath = "",
                PackagesPath = Path.Combine(TestAssetsPath, "packages"),
                BuildEngine = new MockBuildEngine()
            };

            // The task will fail to generate VS components because we have no generated MSI packages.
            // The package feeds are volatile until we actually release and the execution time for the unit tests would spike
            Assert.False(buildTask.Execute());
            ITaskItem missingPack = buildTask.MissingPacks.Where(mp => string.Equals(mp.ItemSpec, "Microsoft.NET.Runtime.MonoAOTCompiler.Task")).FirstOrDefault();

            // This package would be required by the workload, but would be missing
            Assert.Equal(Path.Combine(TestAssetsPath, "packages", "Microsoft.NET.Runtime.MonoAOTCompiler.Task.6.0.0-preview.5.21262.5.nupkg"), missingPack.GetMetadata("SourcePackage"));

            // This package should not show as missing because it has the wrong platform and belongs to an abstract workload
            Assert.DoesNotContain("Microsoft.NETCore.App.Runtime.AOT.osx-x64.Cross.ios-arm", buildTask.MissingPacks.Select(p => p.ItemSpec));
        }
    }
}
