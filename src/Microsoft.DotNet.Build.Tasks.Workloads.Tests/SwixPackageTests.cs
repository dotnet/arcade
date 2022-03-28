// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.Tasks.Workloads.Swix;
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
    }
}
