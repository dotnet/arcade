// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Build.Tasks.Workloads.Swix;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Tests
{
    public class SwixComponentTests : TestBase
    {
        public static readonly ITaskItem[] NoItems = Array.Empty<ITaskItem>();

        public string RandomPath => Path.Combine(AppContext.BaseDirectory, "obj", Path.GetFileNameWithoutExtension(Path.GetTempFileName()));

        [Fact]
        public void ItAssignsDefaultValues()
        {
            WorkloadManifest manifest = Create("WorkloadManifest.json");
            WorkloadDefinition workload = (WorkloadDefinition)manifest.Workloads.FirstOrDefault().Value;
            SwixComponent component = SwixComponent.Create(new ReleaseVersion("6.0.300"), workload, manifest, NoItems, NoItems);

            ComponentSwixProject project = new(component, BaseIntermediateOutputPath, BaseOutputPath);
            string swixProj = project.Create();

            string componentSwr = File.ReadAllText(Path.Combine(Path.GetDirectoryName(swixProj), "component.swr"));
            Assert.Contains("package name=microsoft.net.sdk.blazorwebassembly.aot", componentSwr);

            string componentResSwr = File.ReadAllText(Path.Combine(Path.GetDirectoryName(swixProj), "component.res.swr"));
            Assert.Contains(@"title=""Blazor WebAssembly AOT workload""", componentResSwr);
            Assert.Contains(@"description=""Blazor WebAssembly AOT workload""", componentResSwr);
            Assert.Contains(@"category="".NET""", componentResSwr);
        }

        [Fact]
        public void ItCanOverrideDefaultValues()
        {
            WorkloadManifest manifest = Create("WorkloadManifest.json");
            WorkloadDefinition workload = (WorkloadDefinition)manifest.Workloads.FirstOrDefault().Value;

            ITaskItem[] componentResources = new ITaskItem[]
            {
                new TaskItem("microsoft-net-sdk-blazorwebassembly-aot", new Dictionary<string, string> {
                    { "Title", "AOT" },
                    { "Description", "A long wordy description." },
                    { "Category", "Compilers, build tools, and runtimes" }
                })
            };

            SwixComponent component = SwixComponent.Create(new ReleaseVersion("6.0.300"), workload, manifest, componentResources, NoItems);
            ComponentSwixProject project = new(component, BaseIntermediateOutputPath, BaseOutputPath);
            string swixProj = project.Create();

            string componentResSwr = File.ReadAllText(Path.Combine(Path.GetDirectoryName(swixProj), "component.res.swr"));

            Assert.Contains(@"title=""AOT""", componentResSwr);
            Assert.Contains(@"description=""A long wordy description.""", componentResSwr);
            Assert.Contains(@"category=""Compilers, build tools, and runtimes""", componentResSwr);
        }

        [Fact]
        public void ItCreatesComponentsWhenWorkloadsDoNotIncludePacks()
        {
            WorkloadManifest manifest = Create("mauiWorkloadManifest.json");
            WorkloadDefinition workload = (WorkloadDefinition)manifest.Workloads.FirstOrDefault().Value;
            SwixComponent component = SwixComponent.Create(new ReleaseVersion("7.0.100"), workload, manifest, NoItems, NoItems);
            ComponentSwixProject project = new(component, BaseIntermediateOutputPath, BaseOutputPath);
            string swixProj = project.Create();

            string componentSwr = File.ReadAllText(Path.Combine(Path.GetDirectoryName(swixProj), "component.swr"));
            Assert.Contains(@"vs.dependency id=maui.mobile", componentSwr);
            Assert.Contains(@"vs.dependency id=maui.desktop", componentSwr);
        }

        private static WorkloadManifest Create(string filename)
        {
            return WorkloadManifestReader.ReadWorkloadManifest(Path.GetFileNameWithoutExtension(filename),
                File.OpenRead(Path.Combine(AppContext.BaseDirectory, "testassets", filename)));
        }
    }
}
