// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Tests
{
    public class GenerateWorkloadMsisTests
    {
        public string TestAssetsPath = Path.Combine(AppContext.BaseDirectory, "testassets");

        [Fact]
        public void ItRemovesDuplicateWorkloadPacks()
        {
            TaskItem[] manifests = new[]
            {
                new TaskItem(Path.Combine(TestAssetsPath, "emsdkWorkloadManifest.json")),
                new TaskItem(Path.Combine(TestAssetsPath, "emsdkWorkloadManifest2.json")),
            };

            var packs = GenerateWorkloadMsis.GetWorkloadPacks(manifests).ToArray();

            Assert.Equal(4, packs.Length);
            Assert.Equal("Microsoft.NET.Runtime.Emscripten.Node", packs[0].Id.ToString());
            Assert.Equal("7.0.0-alpha.2.22078.1", packs[0].Version);
            Assert.Equal("Microsoft.NET.Runtime.Emscripten.Node", packs[3].Id.ToString());
            Assert.Equal("7.0.0-alpha.2.22079.1", packs[3].Version);
        }
    }
}
