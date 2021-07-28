// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Tests
{
    public class GenerateManifestMsiTests
    {
        [WindowsOnlyFact]
        public void ItThrowsIfPayloadRelativePathIsTooLong()
        {
            var task = new GenerateManifestMsi();
            task.MsiVersion = "1.2.3.11111";

            Exception e = Assert.Throws<Exception>(() => task.GenerateSwixPackageAuthoring(@"C:\Foo\Microsoft.NET.Workload.Mono.ToolChain.Manifest-6.0.100.6.0.0-preview.7.21377.12-x64.msi",
                "Microsoft.NET.Workload.Mono.ToolChain.Manifest-6.0.100", "x64"));
            Assert.Equal(@"Payload relative path exceeds max length (182): Microsoft.NET.Workload.Mono.ToolChain.Manifest-6.0.100,version=1.2.3,chip=x64,productarch=neutral\Microsoft.NET.Workload.Mono.ToolChain.Manifest-6.0.100.6.0.0-preview.7.21377.12-x64.msi", e.Message);
        }
    }
}
