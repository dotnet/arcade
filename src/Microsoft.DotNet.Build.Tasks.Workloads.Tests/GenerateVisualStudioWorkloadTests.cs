// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Arcade.Test.Common;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Tests
{
    public class GenerateVisualStudioWorkloadTests
    {
        public string IntermediateBaseOutputPath = Path.Combine(AppContext.BaseDirectory, "obj");

        public string TestIntermediateBaseOutputPath => Path.Combine(IntermediateBaseOutputPath, Path.GetFileNameWithoutExtension(Path.GetTempFileName()));

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

                ComponentVersion = "6.5.38766",
                GenerateMsis = false,
                IntermediateBaseOutputPath = TestIntermediateBaseOutputPath,
                WixToolsetPath = "",
                BuildEngine = new MockBuildEngine()
            };

            buildTask.Execute();
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
                ComponentVersion = "6.5.38766",
                GenerateMsis = false,
                IntermediateBaseOutputPath = TestIntermediateBaseOutputPath,
                WixToolsetPath = "",
                BuildEngine = new MockBuildEngine()
            };

            buildTask.Execute();
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

            buildTask.Execute();
            string outputPath = Path.GetDirectoryName(buildTask.SwixProjects[0].GetMetadata("FullPath"));
            string componentSwr = File.ReadAllText(Path.Combine(outputPath, "component.swr"));

            Assert.Single(buildTask.SwixProjects);
            Assert.Contains(@"package name=microsoft.net.sdk.blazorwebassembly.aot
        version=1.0", componentSwr);
            Assert.Contains("vs.dependency id=Microsoft.NET.Runtime.MonoAOTCompiler.Task.6.0.0-preview.4.21201.1", componentSwr);
            Assert.Contains("vs.dependency id=Microsoft.NET.Runtime.Emscripten.Python.6.0.0-preview.4.21205.1", componentSwr);
        }
    }
}
