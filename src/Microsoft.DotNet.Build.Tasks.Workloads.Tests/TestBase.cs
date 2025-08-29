// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Workloads.Msi;
using Microsoft.Arcade.Test.Common;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Tests
{
    public abstract class TestBase
    {
        public static readonly string BaseIntermediateOutputPath = Path.Combine(AppContext.BaseDirectory, "obj", Path.GetFileNameWithoutExtension(Path.GetTempFileName()));
        public static readonly string BaseOutputPath = Path.Combine(AppContext.BaseDirectory, "bin", Path.GetFileNameWithoutExtension(Path.GetTempFileName()));

        public static readonly string MsiOutputPath = Path.Combine(BaseOutputPath, "msi");
        public static readonly string TestAssetsPath = Path.Combine(AppContext.BaseDirectory, "testassets");

        public static readonly string WixToolsetPath = Path.Combine(TestAssetsPath, "wix");

        public static readonly string PackageRootDirectory = Path.Combine(BaseIntermediateOutputPath, "pkg");

        public static readonly string TestOutputRoot = Path.Combine(AppContext.BaseDirectory, "TEST_OUTPUT");

        /// <summary>
        /// Returns a new, random directory for test projects.
        /// </summary>
        public string TestProjectDirectory => Path.Combine(TestOutputRoot, Path.GetFileNameWithoutExtension(Path.GetTempFileName()));

        internal static WorkloadManifestPackage CreateWorkloadManifestPackage(string packageFile, string msiVersion)
        {
            string path = Path.Combine(TestAssetsPath, packageFile);
            TaskItem packageItem = new(path);
            return new(packageItem, PackageRootDirectory, new Version(msiVersion));
        }

        internal static WorkloadManifestMsi CreateWorkloadManifestMsi(string packageFile, string msiVersion, string platform = "x64", string msiOutputPath = null,
            bool isSxS = true)
        {
            WorkloadManifestPackage pkg = CreateWorkloadManifestPackage(packageFile, msiVersion);
            WorkloadManifestMsi msi = new(pkg, platform, new MockBuildEngine(), WixToolsetPath, BaseIntermediateOutputPath,
                isSxS: true);
            return msi;
        }
    }
}
