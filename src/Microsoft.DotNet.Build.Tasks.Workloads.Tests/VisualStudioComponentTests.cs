// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Tests
{
    public class VisualStudioComponentTests
    {
        public static readonly ITaskItem[] NoItems = Array.Empty<ITaskItem>();

        public string RandomPath => Path.Combine(AppContext.BaseDirectory, "obj", Path.GetFileNameWithoutExtension(Path.GetTempFileName()));

        [Fact]
        public void ItAssignsDefaultValues()
        {
            WorkloadManifest manifest = Create("WorkloadManifest.json");
            WorkloadDefinition definition = (WorkloadDefinition)manifest.Workloads.FirstOrDefault().Value;
            VisualStudioComponent component = VisualStudioComponent.Create(null, manifest, definition, NoItems, NoItems, NoItems, NoItems);

            string swixProjDirectory = RandomPath;
            Directory.CreateDirectory(swixProjDirectory);
            component.Generate(swixProjDirectory);

            string componentResSwr = File.ReadAllText(Path.Combine(swixProjDirectory, "component.res.swr"));

            Assert.Contains(@"title=""Blazor WebAssembly AOT workload""", componentResSwr);
            Assert.Contains(@"description=""Blazor WebAssembly AOT workload""", componentResSwr);
            Assert.Contains(@"category="".NET""", componentResSwr);
        }

        [Fact]
        public void ItCanOverrideDefaultValues()
        {
            WorkloadManifest manifest = Create("WorkloadManifest.json");
            WorkloadDefinition definition = (WorkloadDefinition)manifest.Workloads.FirstOrDefault().Value;

            ITaskItem[] resources = new ITaskItem[]
            {
                new TaskItem("microsoft-net-sdk-blazorwebassembly-aot", new Dictionary<string, string> {
                    { "Title", "AOT" },
                    { "Description", "A long wordy description." },
                    { "Category", "Compilers, build tools, and runtimes" }
                })
            };

            VisualStudioComponent component = VisualStudioComponent.Create(null, manifest, definition, NoItems, NoItems, resources, NoItems);

            string swixProjDirectory = RandomPath;
            Directory.CreateDirectory(swixProjDirectory);
            component.Generate(swixProjDirectory);

            string componentResSwr = File.ReadAllText(Path.Combine(swixProjDirectory, "component.res.swr"));

            Assert.Contains(@"title=""AOT""", componentResSwr);
            Assert.Contains(@"description=""A long wordy description.""", componentResSwr);
            Assert.Contains(@"category=""Compilers, build tools, and runtimes""", componentResSwr);
        }

        [Fact]
        public void ItCreatesSafeComponentIds()
        {
            WorkloadManifest manifest = Create("WorkloadManifest.json");
            WorkloadDefinition definition = (WorkloadDefinition)manifest.Workloads.FirstOrDefault().Value;
            VisualStudioComponent component = VisualStudioComponent.Create(null, manifest, definition, NoItems, NoItems, NoItems, NoItems);

            string swixProjDirectory = RandomPath;
            Directory.CreateDirectory(swixProjDirectory);
            component.Generate(swixProjDirectory);

            string componentSwr = File.ReadAllText(Path.Combine(swixProjDirectory, "component.swr"));

            Assert.Contains(@"microsoft.net.sdk.blazorwebassembly.aot", componentSwr);
        }

        [Fact]
        public void ItCreatesComponentsWhenWorkloadsDoNotIncludePacks()
        {
            WorkloadManifest manifest = Create("mauiWorkloadManifest.json");
            WorkloadDefinition definition = (WorkloadDefinition)manifest.Workloads.FirstOrDefault().Value;
            VisualStudioComponent component = VisualStudioComponent.Create(null, manifest, definition, NoItems, NoItems, NoItems, NoItems);

            string swixProjDirectory = RandomPath;
            Directory.CreateDirectory(swixProjDirectory);
            component.Generate(swixProjDirectory);

            string componentSwr = File.ReadAllText(Path.Combine(swixProjDirectory, "component.swr"));

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
