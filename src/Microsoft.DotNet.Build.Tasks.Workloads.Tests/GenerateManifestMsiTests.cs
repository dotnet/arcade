// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Deployment.DotNet.Releases;
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

        [WindowsOnlyTheory]
        [InlineData("a.b.c", "6.0.100", "x86", "{1c857c83-6584-3c77-ab20-7c77f4fdc097}")]
        [InlineData("a.b.c", "6.0.105", "x86", "{1c857c83-6584-3c77-ab20-7c77f4fdc097}")]
        [InlineData("a.b.c", "7.0.105-preview.3.5.6.7.8.9", "x86", "{ca8c5ba1-6937-3237-99d5-cbae7b2b8868}")]
        [InlineData("a.b.c", "7.0.105-preview.3.5.6.7.8.9", "x64", "{da611d31-8a65-3eea-8949-13bcf2a99950}")]
        [InlineData("a.b.c", "7.0.105-preview.4.5.6.7.8.9", "x86", "{e6c1ec79-16d9-3cef-ad7b-0bcefa9636b1}")]
        public void ItGeneratesStableUpgradeCodes(string manifestId, string sdkVersion, string platform, string expectedUpgradeCode)
        {
            ReleaseVersion sdkFeatureBandVersion = GenerateManifestMsi.GetSdkFeatureBandVersion(sdkVersion);
            Guid upgradeCode = GenerateManifestMsi.GenerateUpgradeCode(manifestId, sdkFeatureBandVersion, platform);

            Assert.Equal(expectedUpgradeCode.ToLowerInvariant(), upgradeCode.ToString("B"));
        }

        [WindowsOnlyTheory]
        [InlineData("6.0.100", "6.0.100")]
        [InlineData("6.0.103", "6.0.100")]
        [InlineData("6.0.103-ci", "6.0.100")]
        [InlineData("7.0.1243-preview.8+12345", "7.0.1200-preview.8")]
        public void ItIncludesPrereleasePartsInFeatureBandVersion(string sdkVersion, string expectedFeatureBandVersion)
        {
            ReleaseVersion actual = GenerateManifestMsi.GetSdkFeatureBandVersion(sdkVersion);
            ReleaseVersion expected = new ReleaseVersion(expectedFeatureBandVersion);

            Assert.Equal(expected, actual);
        }

        [WindowsOnlyTheory]
        [InlineData("Microsoft.NET.Workload.Emscripten.Manifest-6.0.100", "6.0.100")]
        [InlineData("Microsoft.NET.Workload.Emscripten-7.0.100", null)]
        [InlineData(null, null)]
        [InlineData("Microsoft.NET.Workload.Emscripten.Manifest-7.3.504-preview.9", "7.3.504-preview.9")]
        public void ItCanExtractTheSdkVersionFromTheManifestPackageId(string packageId, string expectedSdkVersion)
        {
            string actual = GenerateManifestMsi.GetSdkVersionFromPackageId(packageId);

            Assert.Equal(expectedSdkVersion, actual);
        }

        [WindowsOnlyTheory]
        [InlineData("Microsoft.NET.Workload.Emscripten.Manifest-6.0.100", "Microsoft.NET.Workload.Emscripten")]
        [InlineData("Microsoft.NET.Workload.Emscripten-6.0.100", null)]
        public void ItCanExtractTheManifestIdFromTheManifestPackageId(string packageId, string expectedManifestId)
        {
            string actual = GenerateManifestMsi.GetManifestIdFromPackageId(packageId);

            Assert.Equal(expectedManifestId, actual);
        }

        //Microsoft.NET.Workload.Emscripten.Manifest-6.0.100

    }
}
